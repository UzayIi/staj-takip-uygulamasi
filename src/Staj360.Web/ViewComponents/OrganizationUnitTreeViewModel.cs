using Staj360.Domain.Enums;

namespace Staj360.Web.ViewComponents;

public class OrganizationUnitTreeViewModel
{
    public string InputName { get; set; } = "OrganizationUnitId";
    public bool MultiSelect { get; set; }
    public IReadOnlyCollection<Guid> SelectedIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<OrganizationUnitTreeNode> Nodes { get; set; } = Array.Empty<OrganizationUnitTreeNode>();
    public string? SearchPlaceholder { get; set; } = "Birim ara…";
    public string TreeId { get; set; } = "orgUnitTree";
}

public class OrganizationUnitTreeNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public OrganizationUnitType UnitType { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
    public bool IsSelectable { get; set; }
    public string Breadcrumb { get; set; } = string.Empty;
    public List<OrganizationUnitTreeNode> Children { get; set; } = new();
}
