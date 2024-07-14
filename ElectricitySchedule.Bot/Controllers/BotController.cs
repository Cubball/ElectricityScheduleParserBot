using ElectricitySchedule.Bot.Models;
using ElectricitySchedule.Bot.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ElectricitySchedule.Bot.Controllers;

[ApiController]
[Route("")]
public class BotController(
    IUpdateService updateService,
    ITelegramService telegramService) : ControllerBase
{
    // TODO: logging
    private readonly IUpdateService _updateService = updateService;
    private readonly ITelegramService _telegramService = telegramService;

    [HttpPost("update")]
    public async Task<IActionResult> Update(ScheduleModel schedule)
    {
        var updated = await _updateService.UpdateScheduleAsync(schedule);
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