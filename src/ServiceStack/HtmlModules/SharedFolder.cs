#nullable enable
using System;
using ServiceStack.Script;
using ServiceStack.Text;

namespace ServiceStack.HtmlModules;

/// <summary>
/// Register a shared folder to easily import shared .html components or files  
/// &lt;!--shared:Brand,Input--&gt; or /*shared:app.css*/
/// </summary>
public class SharedFolder : IHtmlModulesHandler
{
    public string Name { get; }
    public string SharedDir { get; }
    public string DefaultExt { get; }
    public SharedFolder(string name, string sharedDir, string defaultExt)
    {
        if (string.IsNullOrEmpty(defaultExt))
            throw new ArgumentNullException(nameof(defaultExt));
        if (defaultExt[0] != '.')
            throw new ArgumentNullException(nameof(defaultExt) + " file extension must start with '.'");

        Name = name;
        SharedDir = sharedDir;
        DefaultExt = defaultExt;
    }

    public ReadOnlyMemory<byte> Execute(HtmlModuleContext ctx, string files)
    {
        return ctx.Cache($"{Name}:{SharedDir}/{files}", _ => {
            var sb = StringBuilderCache.Allocate();
            var paths = files.Split(',').Map(file =>
                SharedDir.CombineWith(file + (file.IndexOf('.') == -1 ? DefaultExt : "")));

            foreach (var path in paths)
            {
                var file = ctx.VirtualFiles.GetFile(path);
                sb.AppendLine(file.ReadAllText());
            }

            return StringBuilderCache.ReturnAndFree(sb).AsMemory().ToUtf8();
        });
    }
}
