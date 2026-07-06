namespace ERP.Domain;

/// <summary>
/// Marker type for assembly scanning and test references. The Domain layer is
/// framework- and persistence-ignorant: it must never reference Application,
/// Infrastructure, or Persistence.
/// </summary>
public static class AssemblyMarker;
