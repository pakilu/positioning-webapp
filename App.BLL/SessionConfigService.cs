namespace App.BLL;

public class SessionConfigService
{
    public void ValidateSessionConfig(SessionConfig config)
    {
        var anchors = config.SessionConfigChips
            .Where(x => x.Role == ChipRole.Anchor)
            .ToList();

        var tags = config.SessionConfigChips
            .Where(x => x.Role == ChipRole.Tag)
            .ToList();

        if (anchors.Count < 3)
        {
            throw new Exception("A session configuration must have at least 3 anchors.");
        }

        if (tags.Count < 1)
        {
            throw new Exception("A session configuration must have at least 1 tag.");
        }

        foreach (var anchor in anchors)
        {
            if (anchor.XCoord == null || anchor.YCoord == null)
            {
                throw new Exception("Anchor chips must have coordinates.");
            }
        }

        foreach (var tag in tags)
        {
            if (tag.XCoord != null || tag.YCoord != null || tag.ZCoord != null)
            {
                throw new Exception("Tag chips should not have fixed coordinates.");
            }
        }
    }
}
