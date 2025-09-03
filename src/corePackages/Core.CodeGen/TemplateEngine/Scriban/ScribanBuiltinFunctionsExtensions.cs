﻿using System.Globalization;
using Scriban.Functions;
using Scriban.Runtime;

namespace Core.CodeGen.TemplateEngine.Scriban;

public class ScribanBuiltinFunctionsExtensions : BuiltinFunctions
{
    public ScribanBuiltinFunctionsExtensions()
    {
        addStringFunctionsExtensions();
        addGuidFunctionsExtensions();
    }

    private void addStringFunctionsExtensions()
    {
        ScriptObject stringFunctions = [];
        stringFunctions.Import(
            obj: typeof(ScribanStringFunctionsExtensions),
            renamer: member => member.Name.ToLower(CultureInfo.GetCultureInfo("en-EN"))
        );
        SetValue(member: "string", stringFunctions, readOnly: true);
    }

    private void addGuidFunctionsExtensions()
    {
        ScriptObject guidFunctions = [];
        guidFunctions.Import(
            obj: typeof(ScribanGuidFunctionsExtensions),
            renamer: member => member.Name.ToLower(CultureInfo.GetCultureInfo("en-EN"))
        );
        SetValue(member: "guid", guidFunctions, readOnly: true);
    }
}
