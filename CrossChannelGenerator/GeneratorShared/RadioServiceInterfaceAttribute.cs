// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Visceral;
using Microsoft.CodeAnalysis;

namespace CrossChannel;

public sealed class RadioServiceInterfaceAttributeMock
{
    public static readonly string SimpleName = "RadioServiceInterface";
    public static readonly string StandardName = SimpleName + "Attribute";
    public static readonly string FullName = "CrossChannel." + StandardName;

    public RadioServiceInterfaceAttributeMock(Location location)
    {
        this.Location = location;
    }

    public Location Location { get; }

    public int MaxLinks { get; set; } = int.MaxValue;

    public static RadioServiceInterfaceAttributeMock FromArray(Location location, object?[] constructorArguments, KeyValuePair<string, object?>[] namedArguments)
    {
        var attribute = new RadioServiceInterfaceAttributeMock(location);

        object? val;
        val = VisceralHelper.GetValue(-1, nameof(MaxLinks), constructorArguments, namedArguments);
        if (val != null)
        {
            attribute.MaxLinks = (int)val;
        }

        return attribute;
    }
}
