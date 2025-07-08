using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace final_project_fe.Pages
{
    public class MentorRegulationsModel : PageModel
    {

        [BindProperty]
        public string Action { get; set; }
        public string SuccessMessage { get; set; }
        public string RedirectUrl { get; set; }
        public string Status { get; set; }
        public async Task<IActionResult> OnGetAsync()
        {
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError("", "Bạn chưa đăng nhập.");
                return RedirectToPage("/Login");
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // 🔍 Lấy Role từ token
            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            if (roleClaims.Contains("Mentor"))
            {
                return RedirectToPage("/Index");
            }
            return Page();

        }
        public void OnPost(string action)
        {
            if (action == "agree")
            {
                SuccessMessage = "Thank you for agreeing to our policy! Your application will proceed to the next step.";
                RedirectUrl = "/MentorRegister";
                Status = "Success";
                 RedirectToPage("/MentorRegister");
            }
            else if (action == "disagree")
            {
                SuccessMessage = "We understand your concerns. Please contact us if you have any questions about our policy.";
                RedirectUrl = "";  // Không chuyển hướng
                Status = "Warning";
            }
        }
    }
}
