#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectPSX.Util;

public static class Storage {
    public static bool TryGetExecutable(IEnumerable<string>? args, out string result) {
        result = null!;

        var path = args?.FirstOrDefault();
        if (path == null)
            return false;

        var extension = Path.GetExtension(path).ToLowerInvariant();

        switch (extension) {
            case ".bin":
            case ".cue":
            case ".exe":
                result = path;
                return true;
            default:
                return false;
        }
    }
}
