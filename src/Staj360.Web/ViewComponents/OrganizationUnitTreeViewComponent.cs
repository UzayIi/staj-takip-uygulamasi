using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Services.Organization;
using Staj360.Domain.Enums;

namespace Staj360.Web.ViewComponents;

public class OrganizationUnitTreeViewComponent : ViewComponent
{
    private readonly IOrganizationUnitService _units;

    public OrganizationUnitTreeViewComponent(IOrganizationUnitService units) => _units = units;

    public async Task<IViewComponentResult> InvokeAsync(
        string inputName = "OrganizationUnitId",
        bool multiSelect = false,
        IEnumerable<Guid>? selectedIds = null,
        string? treeId = null,
        CancellationToken cancellationToken = default)
    {
        var all = await _units.ListTreeAsync(cancellationToken);
        var selected = selectedIds?.ToHashSet() ?? new HashSet<Guid>();

        var byParent = all.GroupBy(u => u.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        List<OrganizationUnitTreeNode> Build(Guid? parentId, string parentPath)
        {
            var key = parentId ?? Guid.Empty;
            if (!byParent.TryGetValue(key, out var children))
                return new List<OrganizationUnitTreeNode>();

            return children.Select(u =>
            {
                var path = string.IsNullOrEmpty(parentPath) ? u.Name : $"{parentPath} / {u.Name}";
                var isBranch = u.UnitType == OrganizationUnitType.Branch;
                var node = new OrganizationUnitTreeNode
                {
                    Id = u.Id,
                    Name = u.Name,
                    Code = u.Code,
                    UnitType = u.UnitType,
                    ParentId = u.ParentId,
                    IsActive = u.IsActive,
                    IsSelectable = isBranch && u.IsActive,
                    Breadcrumb = path,
                    Children = Build(u.Id, path)
                };
                return node;
            }).ToList();
        }

        var vm = new OrganizationUnitTreeViewModel
        {
            InputName = inputName,
            MultiSelect = multiSelect,
            SelectedIds = selected,
            TreeId = string.IsNullOrWhiteSpace(treeId) ? "orgUnitTree" : treeId!,
            Nodes = Build(null, string.Empty)
        };

        return View(vm);
    }
}
