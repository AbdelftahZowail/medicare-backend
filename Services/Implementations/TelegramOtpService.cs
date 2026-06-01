using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class TelegramOtpService : ITelegramOtpService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TelegramOtpService(IUnitOfWork unitOfWork, IConfiguration configuration, HttpClient httpClient)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<ApiResponse<bool>> SendOtpAsync(string phoneNumber, string otpCode)
        {
            // 1. Find the Telegram Chat ID for this phone number
            var mapping = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber && !t.IsDeleted);

            if (mapping == null)
            {
                return ApiResponse<bool>.Failure(
                    "لم يتم العثور على حساب تليجرام مرتبط بهذا الرقم. يرجى التفعيل أولاً عبر بوت التليجرام.", 
                    404, 
                    new List<string> { "NoTelegramMapping" }
                );
            }

            // 2. Get Bot Token from appsettings.json
            var botToken = _configuration["TelegramSettings:BotToken"] ?? "YOUR_TELEGRAM_BOT_TOKEN";
            if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_TELEGRAM_BOT_TOKEN")
            {
                // Fallback for development: if no bot token is set, we will just print to console and act as if sent.
                Console.WriteLine($"[TELEGRAM OTP MOCK] Sent OTP {otpCode} to ChatId {mapping.TelegramChatId}");
                return ApiResponse<bool>.Success(true, "تم إرسال كود التفعيل بنجاح (وضع التطوير)");
            }

            // 3. Send message via Telegram Bot API
            var message = $"رمز التحقق الخاص بك في منصة Medical Platform هو: *{otpCode}*\nصالح لمدة 5 دقائق.";
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            
            try
            {
                var payload = new
                {
                    chat_id = mapping.TelegramChatId,
                    text = message,
                    parse_mode = "Markdown"
                };

                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return ApiResponse<bool>.Success(true, "تم إرسال كود التفعيل بنجاح عبر تليجرام");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Telegram API Error] {errorContent}");
                return ApiResponse<bool>.Failure("فشل إرسال الرسالة عبر تليجرام. يرجى المحاولة لاحقاً.", 500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telegram Client Exception] {ex.Message}");
                return ApiResponse<bool>.Failure("حدث خطأ أثناء الاتصال بخدمة تليجرام.", 500);
            }
        }

        public async Task<ApiResponse<bool>> SendNotificationAsync(string phoneNumber, string message)
        {
            // 1. Find the Telegram Chat ID for this phone number
            var mapping = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber && !t.IsDeleted);

            if (mapping == null)
            {
                return ApiResponse<bool>.Failure(
                    "لم يتم العثور على حساب تليجرام مرتبط بهذا الرقم للتبليغ.", 
                    404, 
                    new List<string> { "NoTelegramMapping" }
                );
            }

            // 2. Get Bot Token from appsettings.json
            var botToken = _configuration["TelegramSettings:BotToken"] ?? "YOUR_TELEGRAM_BOT_TOKEN";
            if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_TELEGRAM_BOT_TOKEN")
            {
                Console.WriteLine($"[TELEGRAM NOTIFICATION MOCK] Sent message to ChatId {mapping.TelegramChatId}:\n{message}");
                return ApiResponse<bool>.Success(true, "تم إرسال التنبيه بنجاح (وضع التطوير)");
            }

            // 3. Send message via Telegram Bot API
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            
            try
            {
                var payload = new
                {
                    chat_id = mapping.TelegramChatId,
                    text = message,
                    parse_mode = "Markdown"
                };

                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return ApiResponse<bool>.Success(true, "تم إرسال التنبيه بنجاح عبر تليجرام");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Telegram API Error] {errorContent}");
                return ApiResponse<bool>.Failure("فشل إرسال الرسالة عبر تليجرام.", 500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telegram Client Exception] {ex.Message}");
                return ApiResponse<bool>.Failure("حدث خطأ أثناء الاتصال بخدمة تليجرام.", 500);
            }
        }

        public async Task<ApiResponse<bool>> RegisterChatIdAsync(string phoneNumber, string chatId)
        {
            // Normalize phone number to match the app (e.g. remove spaces, make sure it starts with + or standard format)
            var cleanPhone = phoneNumber.Replace(" ", "").Replace("-", "");
            if (!cleanPhone.StartsWith("+") && !cleanPhone.StartsWith("00"))
            {
                // Assuming Egyptian numbers if no prefix, let's normalize as needed, or just save exactly
            }

            var existing = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == cleanPhone);

            if (existing != null)
            {
                existing.TelegramChatId = chatId;
                _unitOfWork.TelegramMappings.Update(existing);
            }
            else
            {
                var mapping = new TelegramMapping
                {
                    PhoneNumber = cleanPhone,
                    TelegramChatId = chatId
                };
                await _unitOfWork.TelegramMappings.AddAsync(mapping);
            }

            await _unitOfWork.CompleteAsync();
            return ApiResponse<bool>.Success(true, "تم ربط حساب تليجرام بنجاح! يمكنك الآن طلب كود التفعيل.");
        }
    }
}
