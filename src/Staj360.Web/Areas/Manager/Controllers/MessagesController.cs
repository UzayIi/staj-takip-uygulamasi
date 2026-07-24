using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Messaging;
using Staj360.Web.Controllers;

namespace Staj360.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Policy = AppPolicies.ManagerArea)]
public class MessagesController : StaffMessagesControllerBase
{
    public MessagesController(IStaffMessagingService messaging) : base(messaging) { }
    protected override string AreaName => "Manager";
}
