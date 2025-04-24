using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MqttBroker.Database;
using MqttBroker.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MqttBroker.Web.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly BrokerDbContext _context;

        public RegisterModel(BrokerDbContext context)
        {
            _context = context;
        }

        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Username { get; set; }
        [BindProperty] public string Password { get; set; }
        [BindProperty] public string ConfirmPassword { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match!";
                return Page();
            }

            if (_context.Clients.Any(c => c.Username == Username))
            {
                ErrorMessage = "Username already exists!";
                return Page();
            }

            if (_context.Clients.Any(c => c.Email == Email))
            {
                ErrorMessage = "Email already exists!";
                return Page();
            }

            using var sha256 = SHA256.Create();
            var hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(Password)));

            var newClient = new Client
            {
                ClientId = Guid.NewGuid().ToString(),
                Username = Username,
                Email = Email,
                PasswordHash = hash,
                Role = "client"
            };

            _context.Clients.Add(newClient);
            _context.SaveChanges();

            SuccessMessage = "? Registered successfully! You can now log in.";
            return Page();
        }
    }
}
