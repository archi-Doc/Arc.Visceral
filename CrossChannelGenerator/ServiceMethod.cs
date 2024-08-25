// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Text;
using Arc.Visceral;
using Microsoft.CodeAnalysis;

namespace CrossChannel.Generator;

#pragma warning disable SA1602 // Enumeration items should be documented

public class ServiceMethod
{
    public const string VoidName = "void";
    public const string RadioResultName = "CrossChannel.RadioResult<T>";
    public const string TaskName = "System.Threading.Tasks.Task";
    public const string TaskRadioResultName = "System.Threading.Tasks.Task<TResult>";
    public const string CancellationTokenName = "System.Threading.CancellationToken";

    public enum Type
    {
        Other,
        Void,
        RadioResult,
        Task,
        TaskRadioResult,
    }

    public static ServiceMethod? Create(CrossChannelObject obj, CrossChannelObject method)
    {
        var returnObject = method.Method_ReturnObject;
        if (returnObject == null)
        {
            return null;
        }

        var returnType = Type.Other;
        CrossChannelObject? resultObject = null;
        if (returnObject.FullName == VoidName)
        {
            returnType = Type.Void;
        }
        else
        {
            var originalName = returnObject.OriginalDefinition?.FullName ?? string.Empty;
            if (originalName == RadioResultName)
            {
                returnType = Type.RadioResult;
                resultObject = returnObject.Generics_Arguments[0];
            }
            else if (originalName == TaskName)
            {
                returnType = Type.Task;
            }
            else if (originalName == TaskRadioResultName)
            {
                resultObject = returnObject.Generics_Arguments[0];
                if (resultObject.OriginalDefinition?.FullName == RadioResultName)
                {
                    returnType = Type.TaskRadioResult;
                    resultObject = resultObject.Generics_Arguments[0];
                }
            }
        }

        if (returnType == Type.Other)
        {
            method.Body.ReportDiagnostic(CrossChannelBody.Error_MethodReturnType, method.Location);
            return null;
        }

        if (method.Body.Abort)
        {
            return null;
        }

        var serviceMethod = new ServiceMethod(method, returnObject, returnType, resultObject);
        return serviceMethod;
    }

    public ServiceMethod(CrossChannelObject method, CrossChannelObject returnObject, Type returnType, CrossChannelObject? resultObject)
    {
        this.method = method;
        this.ReturnObject = returnObject;
        this.ReturnType = returnType;
        this.ResultObject = resultObject;

        // this.CancellationTokenIndex = this.method.Method_Parameters.IndexOf(CancellationTokenName);
    }

    public Location Location => this.method.Location;

    public string SimpleName => this.method.SimpleName;

    public string LocalName => this.method.LocalName;

    // public WithNullable<CrossChannelObject>? ReturnObject { get; internal set; }

    public string ParameterType { get; private set; } = string.Empty;

    public CrossChannelObject ReturnObject { get; private set; }

    public Type ReturnType { get; private set; }

    public CrossChannelObject? ResultObject { get; private set; }

    public string ResultName => this.ResultObject?.FullName ?? string.Empty;

    // public int CancellationTokenIndex { get; private set; }

    private CrossChannelObject method;

    public string GetParameters()
    {// int a1, string a2
        var sb = new StringBuilder();
        for (var i = 0; i < this.method.Method_Parameters.Length; i++)
        {
            if (i != 0)
            {
                sb.Append(", ");
            }

            sb.Append(this.method.Method_Parameters[i]);
            sb.Append(" a");
            sb.Append(i + 1);
        }

        return sb.ToString();
    }

    public string GetParameterNames()
    {// a1, a2
        var parameters = this.method.Method_Parameters;
        var length = parameters.Length;
        if (length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            if (i != 0)
            {
                sb.Append(", ");
            }

            sb.Append('a');
            sb.Append(i + 1);
        }

        return sb.ToString();
    }
}
