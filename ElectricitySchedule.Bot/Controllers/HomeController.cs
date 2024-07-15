using ElectricitySchedule.Bot.Mapping;
using ElectricitySchedule.Bot.Services;
using ElectricitySchedule.Contracts;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ElectricitySchedule.Bot.Controllers;

[ApiController]
[Route("")]
public class HomeController(
    IUpdateService updateService,
    ITelegramService telegramService) : ControllerBase
{
    private readonly IUpdateService _updateService = updateService;
    private readonly ITelegramService _telegramService = telegramService;

    [HttpPost("update")]
    public async Task<IActionResult> Update(SchedulePayload schedule)
    {
        var updated = await _updateService.UpdateScheduleAsync(schedule.ToModel());
        if (updated)
        {
            await _telegramService.NotifyAboutUpdatesAsync();
        }

        return Ok();
    }

    [HttpPost("message")]
    public async Task<IActionResult> Message(Update update)
    {
        if (update.Type == UpdateType.Message)
        {
            var message = update.Message!;
            await _telegramService.HandleMessage(message.From!.Id, message.Text ?? string.Empty);
        }

        return Ok();
    }
}