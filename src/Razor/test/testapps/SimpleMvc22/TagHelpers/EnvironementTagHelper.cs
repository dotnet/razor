using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SimpleMvc22.TagHelpers
{
    /// <summary>
    /// I made it!
    /// </summary>
    [HtmlTargetElement("environment")]
    public class EnvironmentTagHelper : TagHelper
    {
        /// <summary>
        /// Exclude it!
        /// </summary>
        public string Exclude {get; set;}
    }
}