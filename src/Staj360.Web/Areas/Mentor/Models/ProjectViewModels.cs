using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Web.Areas.Mentor.Models;

public class CreateProjectViewModel
{
    [Required(ErrorMessage = "Proje adı zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Proje Adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    [Display(Name = "Bitiş")]
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "Departman seçiniz.")]
    [Display(Name = "Departman")]
    public Guid DepartmentId { get; set; }

    [Display(Name = "Depo URL")]
    [StringLength(500)]
    public string? RepositoryUrl { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
}

public class ProjectDetailsViewModel
{
    public Project Project { get; set; } = default!;
    public IReadOnlyList<ProjectTask> Tasks { get; set; } = Array.Empty<ProjectTask>();
    public List<SelectListItem> Interns { get; set; } = new();
}

public class CreateTaskViewModel
{
    public Guid ProjectId { get; set; }

    [Required(ErrorMessage = "Görev başlığı zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Öncelik")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [Display(Name = "Atanan Stajyer")]
    public Guid? AssignedInternProfileId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Bitiş Tarihi")]
    public DateOnly? DueDate { get; set; }

    [Display(Name = "Tahmini Süre (dk)")]
    public int? EstimatedMinutes { get; set; }
}
