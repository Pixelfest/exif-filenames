namespace ExifFilenames.Core.Models;

/// <summary>
/// Contains metadata extracted from an image file.
/// </summary>
public class PhotoMetadata
{
    public DateTime? DateOriginal { get; init; }
    public DateTime? DateTimeTaken { get; init; }
    public DateTime? FileDateTime { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }

    /// <summary>
    /// Gets the most accurate available timestamp for the photo:
    /// DateOriginal > DateTimeTaken > FileDateTime > DateTime.MinValue.
    /// </summary>
    public DateTime EffectiveDate
    {
        get
        {
            if (DateOriginal.HasValue && DateOriginal.Value != DateTime.MinValue)
                return DateOriginal.Value;
            if (DateTimeTaken.HasValue && DateTimeTaken.Value != DateTime.MinValue)
                return DateTimeTaken.Value;
            if (FileDateTime.HasValue && FileDateTime.Value != DateTime.MinValue)
                return FileDateTime.Value;
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Returns true if either Make or Model information is present.
    /// </summary>
    public bool HasCameraInfo => !string.IsNullOrWhiteSpace(Make) || !string.IsNullOrWhiteSpace(Model);
}

