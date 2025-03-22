using Spectre.Console;
using Spectre.Console.Rendering;

namespace MassEmailSender;

public class SentColumn : ProgressColumn
{
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        if (task.IsFinished)
        {
            return new Markup(string.Format(
                "[green]{0} {1}[/]",
                task.MaxValue,
                "отправлено"));
        }
        return new Markup(string.Format(
            "{0}[grey]/[/]{1} [grey]{2}[/]",
            task.Value,
            task.MaxValue,
            "отправлено"));
    }
}