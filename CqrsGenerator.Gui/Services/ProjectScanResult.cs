using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Gui.Services;

public sealed record ProjectScanResult(GeneratorConfig Config, ProjectModel Project);
