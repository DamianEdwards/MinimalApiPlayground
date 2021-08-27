# Minimal API Playground
A place I'm trying out the new ASP.NET Core minimal APIs for hosting and HTTP APIs.

## Dependencies
Code in this repo depends on the very latest bits (not even in official previews). If you want to try it out, [grab the latest .NET 6 SDK installer](https://aka.ms/dotnet/6.0.1XX-rc1/daily/dotnet-sdk-win-x64.exe).

**Validation**

First-class support for validation as part of the new minimal APIs will unfortunately not land in .NET 6. However it's fairly straightforward to wire up the validation features found in `System.ComponentModel.Validation` through use of a helper library ([like the example this repo uses](https://github.com/DamianEdwards/MinimalValidation)), or by using an existing validation library like [FluentValidation](https://fluentvalidation.net/).
