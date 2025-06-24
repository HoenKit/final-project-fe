using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace final_project_fe.Pages
{
    public class MentorRegulationsModel : PageModel
    {

        [BindProperty]
        public string Action { get; set; }

        public string SuccessMessage { get; set; }
        public string RedirectUrl { get; set; }
        public string Status { get; set; }
        public void OnGet()
        {
        }
        public void OnPost(string action)
        {
            if (action == "agree")
            {
                SuccessMessage = "Thank you for agreeing to our policy! Your application will proceed to the next step.";
                RedirectUrl = "/Index";
                Status = "Success";
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
