using ERP.Application.Common.Interfaces;
using ERP.Domain.Catalog;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Categories;

public sealed record CategoryDto(
    Guid Id, string Code, string Name, string? Description, Guid? ParentCategoryId, bool IsActive);

// ---- Create ----
public sealed record CreateCategoryCommand(string Code, string Name, string? Description, Guid? ParentCategoryId)
    : IRequest<Result<Guid>>;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (await _db.Categories.AnyAsync(c => c.Code == request.Code, ct))
            return Result.Failure<Guid>(Error.Conflict($"Category '{request.Code}' already exists."));

        if (request.ParentCategoryId is { } parentId
            && !await _db.Categories.AnyAsync(c => c.Id == parentId, ct))
            return Result.Failure<Guid>(Error.Validation("Parent category does not exist."));

        var category = new Category
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return Result.Success(category.Id);
    }
}

// ---- List ----
public sealed record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var items = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CategoryDto(c.Id, c.Code, c.Name, c.Description, c.ParentCategoryId, c.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<CategoryDto>>(items);
    }
}
