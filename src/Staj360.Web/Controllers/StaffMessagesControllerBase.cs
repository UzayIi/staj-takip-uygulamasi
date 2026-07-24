using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Services.Messaging;
using Staj360.Web.Helpers;

namespace Staj360.Web.Controllers;

/// <summary>Yönetici/Danışman mesajlaşma ortak eylemleri.</summary>
public abstract class StaffMessagesControllerBase : Controller
{
    protected readonly IStaffMessagingService Messaging;

    protected StaffMessagesControllerBase(IStaffMessagingService messaging) => Messaging = messaging;

    protected abstract string AreaName { get; }

    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        var items = await Messaging.ListInboxAsync(User.GetUserId(), cancellationToken);
        ViewData["Folder"] = "Inbox";
        ViewData["AreaName"] = AreaName;
        return View("~/Views/Shared/StaffMessages/Inbox.cshtml", items);
    }

    public async Task<IActionResult> Sent(CancellationToken cancellationToken)
    {
        var items = await Messaging.ListSentAsync(User.GetUserId(), cancellationToken);
        ViewData["Folder"] = "Sent";
        ViewData["AreaName"] = AreaName;
        return View("~/Views/Shared/StaffMessages/Sent.cshtml", items);
    }

    public async Task<IActionResult> Unread(CancellationToken cancellationToken)
    {
        var items = await Messaging.ListUnreadAsync(User.GetUserId(), cancellationToken);
        ViewData["Folder"] = "Unread";
        ViewData["AreaName"] = AreaName;
        return View("~/Views/Shared/StaffMessages/Unread.cshtml", items);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await Messaging.GetDetailsAsync(User.GetUserId(), id, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Inbox));
        }
        ViewData["AreaName"] = AreaName;
        return View("~/Views/Shared/StaffMessages/Details.cshtml", result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Compose(CancellationToken cancellationToken)
    {
        ViewData["AreaName"] = AreaName;
        ViewBag.Recipients = await BuildRecipientSelectAsync(cancellationToken);
        ViewBag.UnitsByRecipient = await BuildUnitsMapAsync(cancellationToken);
        return View("~/Views/Shared/StaffMessages/Compose.cshtml", new ComposeStaffMessageForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeStaffMessageForm model, CancellationToken cancellationToken)
    {
        ViewData["AreaName"] = AreaName;
        if (!ModelState.IsValid)
        {
            ViewBag.Recipients = await BuildRecipientSelectAsync(cancellationToken);
            ViewBag.UnitsByRecipient = await BuildUnitsMapAsync(cancellationToken);
            return View("~/Views/Shared/StaffMessages/Compose.cshtml", model);
        }

        var result = await Messaging.SendAsync(User.GetUserId(),
            new SendStaffMessageCommand(model.RecipientUserId, model.OrganizationUnitId, model.Subject, model.Body),
            cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Mesaj gönderilemedi.");
            ViewBag.Recipients = await BuildRecipientSelectAsync(cancellationToken);
            ViewBag.UnitsByRecipient = await BuildUnitsMapAsync(cancellationToken);
            return View("~/Views/Shared/StaffMessages/Compose.cshtml", model);
        }

        TempData["Success"] = "Mesaj gönderildi.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Reply(Guid id, CancellationToken cancellationToken)
    {
        var details = await Messaging.GetDetailsAsync(User.GetUserId(), id, cancellationToken);
        if (!details.Success)
        {
            TempData["Error"] = details.ErrorMessage;
            return RedirectToAction(nameof(Inbox));
        }

        ViewData["AreaName"] = AreaName;
        ViewData["Thread"] = details.Data;
        var form = new ComposeStaffMessageForm
        {
            RecipientUserId = details.Data!.OtherUserId,
            OrganizationUnitId = details.Data.OrganizationUnitId,
            Subject = details.Data.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
                ? details.Data.Subject
                : "Re: " + System.Net.WebUtility.HtmlDecode(details.Data.Subject),
            ParentMessageId = details.Data.Messages.LastOrDefault()?.Id ?? id
        };
        return View("~/Views/Shared/StaffMessages/Reply.cshtml", form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(ComposeStaffMessageForm model, CancellationToken cancellationToken)
    {
        ViewData["AreaName"] = AreaName;
        if (!ModelState.IsValid || !model.ParentMessageId.HasValue)
        {
            TempData["Error"] = "Yanıt gönderilemedi.";
            return RedirectToAction(nameof(Inbox));
        }

        var result = await Messaging.SendAsync(User.GetUserId(),
            new SendStaffMessageCommand(model.RecipientUserId, model.OrganizationUnitId, model.Subject, model.Body, model.ParentMessageId),
            cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id = model.ParentMessageId });
        }

        TempData["Success"] = "Yanıt gönderildi.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await Messaging.ArchiveAsync(User.GetUserId(), id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Mesaj arşivlendi." : result.ErrorMessage;
        return RedirectToAction(nameof(Inbox));
    }

    private async Task<List<SelectListItem>> BuildRecipientSelectAsync(CancellationToken cancellationToken)
    {
        var recipients = await Messaging.GetEligibleRecipientsAsync(User.GetUserId(), cancellationToken);
        return recipients.Select(r => new SelectListItem($"{r.FullName} ({r.Email})", r.UserId.ToString())).ToList();
    }

    private async Task<Dictionary<string, List<SelectListItem>>> BuildUnitsMapAsync(CancellationToken cancellationToken)
    {
        var recipients = await Messaging.GetEligibleRecipientsAsync(User.GetUserId(), cancellationToken);
        return recipients.ToDictionary(
            r => r.UserId.ToString(),
            r => r.SharedUnitIds.Select((id, i) => new SelectListItem(
                i < r.SharedUnitNames.Count ? r.SharedUnitNames[i] : id.ToString(),
                id.ToString())).ToList());
    }
}

public class ComposeStaffMessageForm
{
    public Guid RecipientUserId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? ParentMessageId { get; set; }
}
