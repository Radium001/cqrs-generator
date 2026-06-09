using System;

namespace CqrsGenerator.Gui.Session;

public sealed record GeneratorSessionServices(
    IGenerationSessionNavigator Navigator,
    IServiceProvider ServiceProvider);
