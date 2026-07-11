using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Catalog;

/// <summary>Convierte las opciones del request (DTO) a value objects de dominio, limpiando vacíos.</summary>
internal static class ProductOptions
{
    public static List<ProductOption> From(IEnumerable<ProductOptionDto> options) =>
        options
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => new ProductOption { Name = o.Name.Trim(), Price = o.Price < 0 ? 0 : o.Price })
            .ToList();
}

// ---------------- Categorías ----------------

public sealed class ListCategoriesEndpoint(IRepository<Category> categories, IRMapper mapper)
    : EndpointWithoutRequest<List<CategoryDto>>
{
    public override void Configure() => Get("/categories");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await categories.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<Category, CategoryDto>(list.OrderBy(c => c.SortOrder)), ct);
    }
}

public sealed class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateCategoryValidator : Validator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
        => RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.");
}

public sealed class CreateCategoryEndpoint(IRepository<Category> categories, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<CreateCategoryRequest, CategoryDto>
{
    public override void Configure() => Post("/categories");

    public override async Task HandleAsync(CreateCategoryRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await categories.AnyAsync(c => c.Name == name, ct))
        {
            await HttpContext.SendErrorAsync(Error.Conflict("categorias.duplicada", "Ya existe una categoría con ese nombre."), ct);
            return;
        }

        var all = await categories.ListAsync(ct);
        var category = new Category { Name = name, SortOrder = all.Count == 0 ? 0 : all.Max(c => c.SortOrder) + 1 };
        await categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<Category, CategoryDto>(category), ct);
    }
}

public sealed class UpdateCategoryRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateCategoryValidator : Validator<UpdateCategoryRequest>
{
    public UpdateCategoryValidator()
        => RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.");
}

public sealed class UpdateCategoryEndpoint(IRepository<Category> categories, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<UpdateCategoryRequest, CategoryDto>
{
    public override void Configure() => Put("/categories/{id}");

    public override async Task HandleAsync(UpdateCategoryRequest req, CancellationToken ct)
    {
        if (await categories.GetByIdAsync(req.Id, ct) is not { } category)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("categorias.no_encontrada", "Categoría no encontrada."), ct);
            return;
        }
        var name = req.Name.Trim();
        if (await categories.AnyAsync(c => c.Name == name && c.Id != req.Id, ct))
        {
            await HttpContext.SendErrorAsync(Error.Conflict("categorias.duplicada", "Ya existe una categoría con ese nombre."), ct);
            return;
        }

        category.Name = name;
        categories.Update(category);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Category, CategoryDto>(category), ct);
    }
}

public sealed class DeleteCategoryRequest { public Guid Id { get; set; } }

/// <summary>Elimina una categoría. Bloquea si tiene productos (para no romper el catálogo).</summary>
public sealed class DeleteCategoryEndpoint(IRepository<Category> categories, IRepository<Product> products, IUnitOfWork uow)
    : Endpoint<DeleteCategoryRequest>
{
    public override void Configure() => Delete("/categories/{id}");

    public override async Task HandleAsync(DeleteCategoryRequest req, CancellationToken ct)
    {
        if (await categories.GetByIdAsync(req.Id, ct) is not { } category)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("categorias.no_encontrada", "Categoría no encontrada."), ct);
            return;
        }
        if (await products.AnyAsync(p => p.CategoryId == req.Id, ct))
        {
            await HttpContext.SendErrorAsync(
                Error.Validation("categorias.con_productos", "Esta categoría tiene productos. Muévelos o elimínalos antes de borrarla."), ct);
            return;
        }

        categories.Remove(category);
        await uow.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

// ---------------- Productos ----------------

public sealed class ListProductsRequest
{
    public string? Category { get; set; } // filtro opcional por nombre de categoría
}

public sealed class ListProductsEndpoint(IProductRepository products, IRMapper mapper)
    : Endpoint<ListProductsRequest, List<ProductDto>>
{
    public override void Configure() => Get("/products");

    public override async Task HandleAsync(ListProductsRequest req, CancellationToken ct)
    {
        var list = await products.ListWithCategoryAsync(ct);
        if (!string.IsNullOrWhiteSpace(req.Category))
            list = list.Where(p => p.Category != null &&
                p.Category.Name.Equals(req.Category, StringComparison.OrdinalIgnoreCase)).ToList();

        await Send.OkAsync(mapper.MapList<Product, ProductDto>(list), ct);
    }
}

public sealed class SaveProductRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Tint { get; set; } = "var(--surface-hover)";
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public List<ProductOptionDto> Variants { get; set; } = new();
    public List<ProductOptionDto> Extras { get; set; } = new();
    public bool IsAvailable { get; set; } = true;
}

public sealed class SaveProductValidator : Validator<SaveProductRequest>
{
    public SaveProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del producto es obligatorio.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("La categoría es obligatoria.");
    }
}

public sealed class CreateProductEndpoint(
    IProductRepository products, IRepository<Category> categories, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveProductRequest, ProductDto>
{
    public override void Configure() => Post("/products");

    public override async Task HandleAsync(SaveProductRequest req, CancellationToken ct)
    {
        var allowed = await planService.EnsureCanAddProductAsync(ct);
        if (allowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(allowed.Error, ct);
            return;
        }

        if (await categories.GetByIdAsync(req.CategoryId, ct) is null)
        {
            await HttpContext.SendErrorAsync(Error.Validation("productos.categoria", "La categoría indicada no existe."), ct);
            return;
        }

        var product = new Product
        {
            Name = req.Name.Trim(), Price = req.Price, Emoji = req.Emoji, Tint = req.Tint,
            Description = req.Description.Trim(), CategoryId = req.CategoryId,
            Variants = ProductOptions.From(req.Variants), Extras = ProductOptions.From(req.Extras), IsAvailable = req.IsAvailable,
        };
        await products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await products.GetWithCategoryAsync(product.Id, ct) ?? product;
        await Send.OkAsync(mapper.Map<Product, ProductDto>(saved), ct);
    }
}

public sealed class UpdateProductEndpoint(
    IProductRepository products, IRepository<Category> categories, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveProductRequest, ProductDto>
{
    public override void Configure() => Put("/products/{id}");

    public override async Task HandleAsync(SaveProductRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await products.GetByIdAsync(id, ct) is not { } product)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("productos.no_encontrado", "Producto no encontrado."), ct);
            return;
        }
        if (await categories.GetByIdAsync(req.CategoryId, ct) is null)
        {
            await HttpContext.SendErrorAsync(Error.Validation("productos.categoria", "La categoría indicada no existe."), ct);
            return;
        }

        product.Name = req.Name.Trim();
        product.Price = req.Price;
        product.Emoji = req.Emoji;
        product.Tint = req.Tint;
        product.Description = req.Description.Trim();
        product.CategoryId = req.CategoryId;
        product.Variants = ProductOptions.From(req.Variants);
        product.Extras = ProductOptions.From(req.Extras);
        product.IsAvailable = req.IsAvailable;
        products.Update(product);
        await uow.SaveChangesAsync(ct);

        var saved = await products.GetWithCategoryAsync(product.Id, ct) ?? product;
        await Send.OkAsync(mapper.Map<Product, ProductDto>(saved), ct);
    }
}

public sealed class ToggleAvailabilityRequest
{
    public Guid Id { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class ToggleAvailabilityEndpoint(IProductRepository products, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<ToggleAvailabilityRequest, ProductDto>
{
    public override void Configure() => Patch("/products/{id}/availability");

    public override async Task HandleAsync(ToggleAvailabilityRequest req, CancellationToken ct)
    {
        if (await products.GetByIdAsync(req.Id, ct) is not { } product)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("productos.no_encontrado", "Producto no encontrado."), ct);
            return;
        }

        product.IsAvailable = req.IsAvailable;
        products.Update(product);
        await uow.SaveChangesAsync(ct);

        var saved = await products.GetWithCategoryAsync(product.Id, ct) ?? product;
        await Send.OkAsync(mapper.Map<Product, ProductDto>(saved), ct);
    }
}

public sealed class DeleteProductRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteProductEndpoint(IProductRepository products, IUnitOfWork uow)
    : Endpoint<DeleteProductRequest>
{
    public override void Configure() => Delete("/products/{id}");

    public override async Task HandleAsync(DeleteProductRequest req, CancellationToken ct)
    {
        if (await products.GetByIdAsync(req.Id, ct) is not { } product)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("productos.no_encontrado", "Producto no encontrado."), ct);
            return;
        }

        products.Remove(product);
        await uow.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
