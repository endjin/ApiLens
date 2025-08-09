namespace ApiLens.Core.Models;

/// <summary>
/// Represents the type of project file.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Solution file (.sln).
    /// </summary>
    Solution,

    /// <summary>
    /// C# project file (.csproj).
    /// </summary>
    CsProj,

    /// <summary>
    /// F# project file (.fsproj).
    /// </summary>
    FsProj,

    /// <summary>
    /// Visual Basic project file (.vbproj).
    /// </summary>
    VbProj
}