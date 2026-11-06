using Microsoft.AspNetCore.Mvc;
using LegalApp.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;

namespace LegalApp.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IConfiguration config, ILogger<ContactController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ContactRequest req)
    {
        // Отладка — что пришло
        _logger.LogInformation(
            "Запрос: Situation={S}, Format={F}, Name={N}, Phone={P}, Consent={C}",
            req?.Situation, req?.Format, req?.Name, req?.Phone, req?.Consent);

        if (req == null)
        {
            _logger.LogWarning("req = null");
            return BadRequest(new { error = "Пустой запрос" });
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => k.Key,
                    v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            _logger.LogWarning("Ошибки валидации: {@Errors}", errors);
            return BadRequest(new { error = "Ошибка валидации", details = errors });
        }

        if (!req.Consent)
        {
            _logger.LogWarning("Consent = false");
            return BadRequest(new { error = "Не отмечено согласие" });
        }

        // SMTP-настройки
        var host = _config["Smtp:Host"];
        var port = int.Parse(_config["Smtp:Port"] ?? "465");
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Password"];
        var to = _config["Smtp:To"];
        var fromName = _config["Smtp:FromName"] ?? "ЮриСтПрофи26";

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(pass)
            || string.IsNullOrWhiteSpace(to))
        {
            _logger.LogError("SMTP не настроен в appsettings.json");
            return StatusCode(500, new { error = "Почта не настроена" });
        }

        var body = $@"
            <div style='font-family:Arial,sans-serif;font-size:14px;color:#222;line-height:1.6;'>
                <h2 style='color:#1a076f;margin:0 0 16px;'>Новая заявка с сайта ЮриСтПрофи26</h2>

                <p style='margin:0 0 8px;'><b>Имя:</b> {WebUtility.HtmlEncode(req.Name)}</p>
                <p style='margin:0 0 8px;'><b>Телефон:</b> {WebUtility.HtmlEncode(req.Phone)}</p>
                <p style='margin:0 0 8px;'><b>Email:</b> {WebUtility.HtmlEncode(req.Email ?? "—")}</p>

                <hr style='border:none;border-top:1px solid #ddd;margin:16px 0;'>

                <p style='margin:0 0 8px;'><b>Ситуация:</b> {WebUtility.HtmlEncode(req.Situation)}</p>
                <p style='margin:0 0 8px;'><b>Формат:</b> {WebUtility.HtmlEncode(req.Format)}</p>
                <p style='margin:0 0 8px;'><b>Срочность:</b> {WebUtility.HtmlEncode(req.Urgency ?? "—")}</p>
                <p style='margin:0 0 8px;'><b>Бюджет:</b> {WebUtility.HtmlEncode(req.Budget ?? "—")}</p>

                <hr style='border:none;border-top:1px solid #ddd;margin:16px 0;'>

                <p style='margin:0 0 8px;'><b>Комментарий:</b></p>
                <p style='margin:0;white-space:pre-wrap;'>{WebUtility.HtmlEncode(req.Comment ?? "—")}</p>

                <p style='color:#888;font-size:12px;margin-top:24px;'>
                    Отправлено: {DateTime.Now:dd.MM.yyyy HH:mm}
                </p>
            </div>";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, user));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = $"Заявка: {req.Situation} — {req.Name}";

            message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000;

            await client.ConnectAsync(host, port, SecureSocketOptions.Auto);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки письма");
            return StatusCode(500, new { error = "Ошибка отправки" });
        }
    }
}