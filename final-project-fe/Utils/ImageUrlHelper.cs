namespace final_project_fe.Utils
{
    public static class ImageUrlHelper
    {
        public static string AppendSasTokenIfNeeded(string imageUrl, string sasToken)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return string.Empty;

            if (imageUrl.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                if (!imageUrl.Contains("?", StringComparison.Ordinal))
                {
                    return $"{imageUrl}?{sasToken}";
                }
            }

            return imageUrl;
        }
        public static string RemoveSasTokenIfBlob(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return string.Empty;

            var isBlob = imageUrl.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase);

            if (isBlob)
            {
                int index = imageUrl.IndexOf('?');
                if (index > -1)
                    return imageUrl.Substring(0, index);
            }

            return imageUrl;
        }

    }

}
