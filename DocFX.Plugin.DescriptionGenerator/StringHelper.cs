namespace Docfx.Plugin.DescriptionGenerator;

public static class StringHelper
{
    /// <summary>
    ///     Truncates the specified string.
    /// </summary>
    /// <remarks>
    ///     This method truncates the input string.
    ///
    ///     This snippet is fetched from the Humanizer project, which is licensed under the MIT license.
    ///     Copyright(c) .NET Foundation and Contributors
    /// </remarks>
    /// <seealso href="https://github.com/Humanizr/Humanizer/" />
    /// <seealso href="https://github.com/Humanizr/Humanizer/blob/master/LICENSE" />
    public static string Truncate(this string value, int length, string truncationString)
    {
        if (value == null)
            return null;

        if (value.Length == 0)
            return value;

        if (truncationString == null || truncationString.Length > length)
            return value.Substring(0, length);

        return value.Length > length
            ? value.Substring(0, length - truncationString.Length) + truncationString
            : value;
    }
}