using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/user-activities")]
public sealed class UserActivitiesController : ControllerBase
{
    private readonly IUserActivityRepository _activityRepository;

    public UserActivitiesController(IUserActivityRepository activityRepository)
    {
        _activityRepository = activityRepository;
    }

    [HttpPost]
    public async Task<IActionResult> RecordActivity(UserActivityRequest request)
    {
        await _activityRepository.RecordAsync(request);
        return Accepted(new { message = "Activity recorded." });
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<UserActivity>>> GetRecent(int userId, [FromQuery] int take = 50)
    {
        var activities = await _activityRepository.GetRecentAsync(userId, take);
        return Ok(activities);
    }
}
