using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public static class UtilityService
{

    private static readonly ILogger Logger = new LoggerFactory().CreateLogger("UtilityService");
    private static IConfiguration Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();


    public static void Initialize(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public static async Task SendEmailAsync(string recipient, string subject, string body, List<string> ccRecipients = null)
    {
        try
        {
            var smtpClient = new SmtpClient(Configuration["Smtp:Host"])
            {
                Port = int.Parse(Configuration["Smtp:Port"]),
                Credentials = new NetworkCredential(Configuration["Smtp:Username"], Configuration["Smtp:Password"]),
                EnableSsl = true, //Configuration["Smtp:EnableSsl"] == "true"
            };

            // Create the email message
            var message = new MailMessage(Configuration["Smtp:SenderEmail"], recipient, subject, body)
            {
                IsBodyHtml = true
            };

            // Add CC recipients if any
            if (ccRecipients != null)
            {
                foreach (var ccRecipient in ccRecipients)
                {
                    message.CC.Add(ccRecipient);
                }
            }

            // Send the email
            await smtpClient.SendMailAsync(message);
            // Log successful email sending
            Logger.LogInformation($"Email sent to {recipient} with subject: {subject}");
        }
        catch (Exception ex)
        {
            // Log email sending failure
            Logger.LogError(ex, $"Failure sending email to {recipient} with subject: {subject}");
        }

    }

    public static async Task<string> UploadProfilePictureAsync(IFormFile fileToUpload)
    {
        // get settings from appsettings.json
        var PublicPath = Configuration["UploadSettings:PublicPath"];
        var UploadPath = Configuration["UploadSettings:UploadPath"];
        // var AllowedProfileExtensions = Configuration["UploadSettings:AllowedProfileExtensions"].Split(',');
        var MaxSize = int.Parse(Configuration["UploadSettings:MaxSize"]);

        // check max file size
        if (fileToUpload.Length > MaxSize * 1024 * 1024)
        {
            return $"The file size is too large. The maximum file size allowed is {MaxSize}MB";
        }

        // check file extension
        var extension = Path.GetExtension(fileToUpload.FileName);
        // if (!AllowedProfileExtensions.Contains(extension.ToLower()))
        // {
        //     return $" The file extension {extension} is not allowed only {string.Join(",", AllowedProfileExtensions)} are allowed";
        // }

        // generate file name
        var fileName = Guid.NewGuid().ToString() + extension;

        // create directory if it does not exist
        if (!Directory.Exists(UploadPath))
        {
            Directory.CreateDirectory(UploadPath);
        }

        // create full path
        var fullPath = Path.Combine(UploadPath, fileName);

        // create file stream
        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            // copy file to stream
            await fileToUpload.CopyToAsync(fileStream);
        }

        // return the public path
        return $"{PublicPath}/{fileName}";

    }



    public static void LogError(string errorMessage)
    {
        // Log the error
        Logger.LogError(errorMessage);
    }

    // Log Exceptions
    public static void LogException(Exception ex)
    {
        // Log the exception
        Logger.LogError(ex, ex.Message);
    }

    internal static JwtSecurityToken GenerateToken(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.Now.AddHours(Convert.ToDouble(Configuration["Jwt:ExpireDays"]));

        var token = new JwtSecurityToken(
            issuer: Configuration["Jwt:Issuer"],
            audience: Configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return token;
    }

    private static JwtSecurityToken ExtractTokenFromRequest(HttpRequest request)
    {
        var token = request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    internal static string GetUserIdFromToken(HttpRequest request)
    {
        var token = ExtractTokenFromRequest(request);
        var userId = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        return userId.ToString();
    }
    internal static LoginTokenDTO GetUserIdFromCurrentRequest(HttpRequest request)
    {
        var token = ExtractTokenFromRequest(request);
        var userId = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var role = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        var institutionId = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == "InstitutionId")?.Value;
        var institutionName = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == "InstitutionName")?.Value;
        var isSuperAdmin = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == "IsSuperAdmin")?.Value;
        return new LoginTokenDTO
        {
            Name = name,
            Email = email,
            token = token.ToString(),
            IsSuperAdmin = isSuperAdmin == "true",
            Role = role,
            InstitutionId = institutionId,
            InstitutionName = institutionName,
            jwtSecurityToken = token
        };

    }

    internal static string GetEmailFromToken(object token)
    {
        return ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "";
    }

    // Is PasswordResetToken Valid (Has valid User Id and Email)

    internal static bool IsTokenValid(object token)
    {
        var userId = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = ((JwtSecurityToken)token).Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        return userId != null && email != null;
    }

    // Receives an Exception, Checks for InnderExceptions and Returns a List of Error Messages 

    internal static List<string> HandleException(Exception ex)
    {
        var errorMessages = new List<string>();
        if (ex.InnerException != null)
        {
            errorMessages.Add(ex.InnerException.Message);
            if (ex.InnerException.InnerException != null)
            {
                errorMessages.Add(ex.InnerException.InnerException.Message);
            }
        }
        else
        {
            errorMessages.Add(ex.Message);
        }

        return errorMessages;
    }


    // Is DTO Valid (Has no Data Annotation errors)
    internal static bool IsDTOValid(object dto)
    {
        var context = new ValidationContext(dto, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, context, results, true);
        return isValid;
    }

    // Fetch Data Annotation Errors
    internal static List<string> FetchDataAnnotationErrors(object dto)
    {
        var context = new ValidationContext(dto, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, context, results, true);
        var errorMessages = new List<string>();
        if (!isValid)
        {
            foreach (var validationResult in results)
            {
                errorMessages.Add(validationResult.ErrorMessage);
            }
        }
        return errorMessages;
    }



    // Generate random password given length of password
    internal static string GenerateRandomPassword(int length)
    {
        const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        var res = new StringBuilder();
        var rnd = new Random();
        while (0 < length--)
        {
            res.Append(valid[rnd.Next(valid.Length)]);
        }
        return res.ToString();
    }



    // Add user to Default Competition which is the Global Competition
    internal static async Task AddUserToDefaultCompetitionAsync(string userId)
    {

    }





}