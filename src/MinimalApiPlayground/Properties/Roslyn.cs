// Code taken from https://github.com/dotnet/roslyn/blob/7d7bf0cc73e335390d73c9de6d7afd1e49605c9d/src/Compilers/Test/Utilities/CSharp/FunctionPointerUtilities.cs#L335
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

static class Roslyn
{
    internal static class GeneratedNameParser
    {
        // Parse the generated name. Returns true for names of the form
        // [CS$]<[middle]>c[__[suffix]] where [CS$] is included for certain
        // generated names, where [middle] and [__[suffix]] are optional,
        // and where c is a single character in [1-9a-z]
        // (csharp\LanguageAnalysis\LIB\SpecialName.cpp).
        internal static bool TryParseGeneratedName(
            string name,
            out GeneratedNameKind kind,
            out int openBracketOffset,
            out int closeBracketOffset)
        {
            openBracketOffset = -1;
            if (name.StartsWith("CS$<", StringComparison.Ordinal))
            {
                openBracketOffset = 3;
            }
            else if (name.StartsWith("<", StringComparison.Ordinal))
            {
                openBracketOffset = 0;
            }

            if (openBracketOffset >= 0)
            {
                closeBracketOffset = IndexOfBalancedParenthesis(name, openBracketOffset, '>');
                if (closeBracketOffset >= 0 && closeBracketOffset + 1 < name.Length)
                {
                    int c = name[closeBracketOffset + 1];
                    if (c is >= '1' and <= '9' or >= 'a' and <= 'z') // Note '0' is not special.
                    {
                        kind = (GeneratedNameKind)c;
                        return true;
                    }
                }
            }

            kind = GeneratedNameKind.None;
            openBracketOffset = -1;
            closeBracketOffset = -1;
            return false;
        }

        private static int IndexOfBalancedParenthesis(string str, int openingOffset, char closing)
        {
            char opening = str[openingOffset];

            int depth = 1;
            for (int i = openingOffset + 1; i < str.Length; i++)
            {
                var c = str[i];
                if (c == opening)
                {
                    depth++;
                }
                else if (c == closing)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Parses generated local function name out of a generated method name.
        /// </summary>
        internal static bool TryParseLocalFunctionName(string generatedName, out string? localFunctionName)
        {
            localFunctionName = null;

            // '<' containing-method-name '>' 'g' '__' local-function-name '|' method-ordinal '_' lambda-ordinal
            if (!TryParseGeneratedName(generatedName, out var kind, out _, out int closeBracketOffset) || kind != GeneratedNameKind.LocalFunction)
            {
                return false;
            }

            int localFunctionNameStart = closeBracketOffset + 2 + GeneratedNameConstants.SuffixSeparator.Length;
            if (localFunctionNameStart >= generatedName.Length)
            {
                return false;
            }

            int localFunctionNameEnd = generatedName.IndexOf(GeneratedNameConstants.LocalFunctionNameTerminator, localFunctionNameStart);
            if (localFunctionNameEnd < 0)
            {
                return false;
            }

            localFunctionName = generatedName.Substring(localFunctionNameStart, localFunctionNameEnd - localFunctionNameStart);
            return true;
        }
    }

    internal static class GeneratedNameConstants
    {
        internal const char DotReplacementInTypeNames = '-';
        internal const string SynthesizedLocalNamePrefix = "CS$";
        internal const string SuffixSeparator = "__";
        internal const char LocalFunctionNameTerminator = '|';
    }

    internal enum GeneratedNameKind
    {
        None = 0,

        // Used by EE:
        ThisProxyField = '4',
        HoistedLocalField = '5',
        DisplayClassLocalOrField = '8',
        LambdaMethod = 'b',
        LambdaDisplayClass = 'c',
        StateMachineType = 'd',
        LocalFunction = 'g', // note collision with Deprecated_InitializerLocal, however this one is only used for method names

        // Used by EnC:
        AwaiterField = 'u',
        HoistedSynthesizedLocalField = 's',

        // Currently not parsed:
        StateMachineStateField = '1',
        IteratorCurrentBackingField = '2',
        StateMachineParameterProxyField = '3',
        ReusableHoistedLocalField = '7',
        LambdaCacheField = '9',
        FixedBufferField = 'e',
        AnonymousType = 'f',
        TransparentIdentifier = 'h',
        AnonymousTypeField = 'i',
        AnonymousTypeTypeParameter = 'j',
        AutoPropertyBackingField = 'k',
        IteratorCurrentThreadIdField = 'l',
        IteratorFinallyMethod = 'm',
        BaseMethodWrapper = 'n',
        AsyncBuilderField = 't',
        DynamicCallSiteContainerType = 'o',
        DynamicCallSiteField = 'p',
        AsyncIteratorPromiseOfValueOrEndBackingField = 'v',
        DisposeModeField = 'w',
        CombinedTokensField = 'x', // last

        // Deprecated - emitted by Dev12, but not by Roslyn.
        // Don't reuse the values because the debugger might encounter them when consuming old binaries.
        [Obsolete]
        Deprecated_OuterscopeLocals = '6',
        [Obsolete]
        Deprecated_IteratorInstance = 'a',
        [Obsolete]
        Deprecated_InitializerLocal = 'g',
        [Obsolete]
        Deprecated_DynamicDelegate = 'q',
        [Obsolete]
        Deprecated_ComrefCallLocal = 'r',
    }
}