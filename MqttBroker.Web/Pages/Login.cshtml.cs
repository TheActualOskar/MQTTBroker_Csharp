using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MqttBroker.Models;
using MqttBroker.Database;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MqttBroker.Web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly BrokerDbContext _context;

        public LoginModel(BrokerDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            var user = _context.Clients.FirstOrDefault(c => c.Username == Username);

            if (user == null || !VerifyPassword(Password, user.PasswordHash))
            {
                ErrorMessage = "Invalid username or password";
                return Page();
            }

            // ? Store the username in session so other pages can use it
            HttpContext.Session.SetString("Username", user.Username);

            if (user.Role == "admin")
                return RedirectToPage("/AdminDashboard");
            else
                return RedirectToPage("/ClientDashboard");
        }

        private bool VerifyPassword(string inputPassword, string storedHash)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputPassword));
            var inputHash = Convert.ToBase64String(hashBytes);
            return storedHash == inputHash;
        }
    }
}
