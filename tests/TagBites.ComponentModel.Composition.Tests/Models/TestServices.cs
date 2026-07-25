using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace TagBites.ComponentModel.Composition.Tests.Models;

public interface ITestService
{
    string Name { get; }
}

[Export(typeof(ITestService))]
public class AlphaService : ITestService
{
    public string Name => "Alpha";
}

[Export(typeof(ITestService))]
public class BetaService : ITestService
{
    public string Name => "Beta";
}

[Export("Named", typeof(ITestService))]
public class NamedService : ITestService
{
    public string Name => "Named";
}

public interface IDuplicateService;

[Export(typeof(IDuplicateService))]
[Guid("8B1D9A15-2F5C-4C6B-9E3A-3C1D3B6C7A01")]
public class DuplicateFirstService : IDuplicateService;

[Export(typeof(IDuplicateService))]
[Guid("8B1D9A15-2F5C-4C6B-9E3A-3C1D3B6C7A01")]
public class DuplicateSecondService : IDuplicateService;
