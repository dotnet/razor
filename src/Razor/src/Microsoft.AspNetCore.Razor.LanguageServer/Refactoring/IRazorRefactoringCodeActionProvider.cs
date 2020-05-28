using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;


namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    struct RefactoringContext
    {
        public readonly CodeActionParams Request;
        public readonly RazorCodeDocument Document;
        public readonly SourceLocation Location;

        public RefactoringContext(CodeActionParams request, RazorCodeDocument document, SourceLocation location)
        {
            this.Request = request;
            this.Document = document;
            this.Location = location;
        }
    }

    interface IRazorRefactoringCodeActionProvider
    {
        public Task<CommandOrCodeActionContainer> Provide(RefactoringContext context, CancellationToken cancellationToken);
    }
}
