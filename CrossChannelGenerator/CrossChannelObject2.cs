// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Visceral;

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1602 // Enumeration items should be documented
#pragma warning disable SA1611

namespace CrossChannel.Generator;

public partial class CrossChannelObject
{
    internal void GenerateBrokerClass(ScopingStringBuilder ssb)
    {
        var accessModifier = this.ContainingObject is null ? "internal" : "private";
        using (ssb.ScopeBrace($"{accessModifier} class {this.ClassName} : {this.LocalName}"))
        {
            ssb.AppendLine($"private readonly Channel<{this.LocalName}> channel;");
            using (ssb.ScopeBrace($"public {this.ClassName}(object channel)"))
            {
                ssb.AppendLine($"this.channel = (Channel<{this.LocalName}>)channel;");
            }

            if (this.Methods is not null)
            {
                foreach (var x in this.Methods)
                {
                    if (x.ReturnType == ServiceMethod.Type.Other)
                    {
                        continue;
                    }

                    var isAsync = (x.ReturnType == ServiceMethod.Type.Task || x.ReturnType == ServiceMethod.Type.TaskRadioResult) ? "async " : string.Empty;
                    using (ssb.ScopeBrace($"{isAsync}{x.ReturnObject.FullName} {this.LocalName}.{x.SimpleName}({x.GetParameters()})"))
                    {
                        if (x.ReturnType == ServiceMethod.Type.Void)
                        {
                            this.GenerateBrokerMethod_Void(ssb, x);
                        }
                        else if (x.ReturnType == ServiceMethod.Type.RadioResult)
                        {
                            this.GenerateBrokerMethod_RadioResult(ssb, x);
                        }
                        else if (x.ReturnType == ServiceMethod.Type.Task)
                        {
                            this.GenerateBrokerMethod_Task(ssb, x);
                        }
                        else if (x.ReturnType == ServiceMethod.Type.TaskRadioResult)
                        {
                            this.GenerateBrokerMethod_TaskRadioResult(ssb, x);
                        }
                    }
                }
            }
        }
    }

    private void GenerateBrokerMethod_Void(ScopingStringBuilder ssb, ServiceMethod method)
    {// void
        this.Generate_GetList(ssb);

        var forScope = this.Generate_ForEach(ssb);
        ssb.AppendLine($"instance.{method.SimpleName}({method.GetParameterNames()});");

        forScope.Dispose();
    }

    private void GenerateBrokerMethod_RadioResult(ScopingStringBuilder ssb, ServiceMethod method)
    {// RadioResult<T>
        this.Generate_GetList(ssb);
        ssb.AppendLine($"{method.ResultName} firstResult = default!;");
        ssb.AppendLine($"{method.ResultName}[]? results = default;");
        ssb.AppendLine("var count = 0;");

        var forScope = this.Generate_ForEach(ssb);
        using (ssb.ScopeBrace($"if (instance.{method.SimpleName}({method.GetParameterNames()}).TryGetSingleResult(out var r))"))
        {
            using (ssb.ScopeBrace("if (count == 0)"))
            {
                ssb.AppendLine("count = 1;");
                ssb.AppendLine("firstResult = r;");
            }

            using (ssb.ScopeBrace("else"))
            {
                using (ssb.ScopeBrace("if (results is null)"))
                {
                    ssb.AppendLine($"results = new {method.ResultName}[countHint];");
                    ssb.AppendLine("results[0] = firstResult;");
                }

                ssb.AppendLine("if (count < countHint) results[count++] = r;");
                ssb.AppendLine("else break;");
            }
        }

        forScope.Dispose();

        ssb.AppendLine("if (count == 0) return default;");
        ssb.AppendLine("else if (count == 1) return new(firstResult);");
        ssb.AppendLine("else if (countHint != count) Array.Resize(ref results, count);");
        ssb.AppendLine("return new(results!);");
    }

    private void GenerateBrokerMethod_Task(ScopingStringBuilder ssb, ServiceMethod method)
    {// Task
        this.Generate_GetList(ssb);
        ssb.AppendLine($"var tasks = new Task[countHint];");
        ssb.AppendLine("var count = 0;");

        var forScope = this.Generate_ForEach(ssb);
        ssb.AppendLine($"if (count < countHint) tasks[count++] = instance.{method.SimpleName}({method.GetParameterNames()});");
        ssb.AppendLine("else break;");

        forScope.Dispose();

        ssb.AppendLine("if (countHint != count) Array.Resize(ref tasks, count);");
        ssb.AppendLine("if (count == 0) return;");
        ssb.AppendLine("else if (count == 1) await tasks[0].ConfigureAwait(false);");
        ssb.AppendLine("else await Task.WhenAll(tasks).ConfigureAwait(false);");
    }

    private void GenerateBrokerMethod_TaskRadioResult(ScopingStringBuilder ssb, ServiceMethod method)
    {// Task<RadioResult<T>>
        this.Generate_GetList(ssb);
        ssb.AppendLine($"var tasks = new {method.ReturnObject.FullName}[countHint];");
        ssb.AppendLine("var count = 0;");

        var forScope = this.Generate_ForEach(ssb);
        ssb.AppendLine($"if (count < countHint) tasks[count++] = instance.{method.SimpleName}({method.GetParameterNames()});");
        ssb.AppendLine("else break;");

        forScope.Dispose();

        ssb.AppendLine("if (countHint != count) Array.Resize(ref tasks, count);");
        ssb.AppendLine("if (count == 0) return default;");
        ssb.AppendLine("else if (count == 1) return await tasks[0].ConfigureAwait(false);");
        ssb.AppendLine("else return new((await Task.WhenAll(tasks).ConfigureAwait(false)).Select(x => x.TryGetSingleResult(out var r) ? r : default).ToArray());");
    }

    private void Generate_GetList(ScopingStringBuilder ssb)
        => ssb.AppendLine("(var array, var countHint) = this.channel.InternalGetList();");

    private ScopingStringBuilder.IScope Generate_ForEach(ScopingStringBuilder ssb)
    {
        var scope = ssb.ScopeBrace("foreach (var x in array)");
        ssb.AppendLine("if (x is null) continue;");
        ssb.AppendLine("if (!x.TryGetInstance(out var instance)) { x.Dispose(); continue; }");
        // ssb.AppendLine("var instance = x.Instance;");

        return scope;
    }
}
