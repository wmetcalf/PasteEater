using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace PasteEater
{
    /// <summary>
    /// Represents different types of clipboard content targets for rule matching
    /// </summary>
    public class ClipboardTargets
    {
        /// <summary>
        /// Original raw clipboard text
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// Normalized text with quotes and special characters removed
        /// </summary>
        public string Normalized { get; set; } = string.Empty;

        /// <summary>
        /// Collection of all decoded strings from all decoders
        /// </summary>
        public List<string> All { get; } = new List<string>();

        /// <summary>
        /// Collection of strings specifically decoded from Base64
        /// </summary>
        public List<string> Base64 { get; } = new List<string>();

        /// <summary>
        /// Collection of strings specifically decoded from Hex
        /// </summary>
        public List<string> Hex { get; } = new List<string>();

        /// <summary>
        /// Collection of strings specifically decoded from Decimal
        /// </summary>
        public List<string> Decimal { get; } = new List<string>();

        /// <summary>
        /// Detailed decoded string results with metadata about decoding method
        /// </summary>
        public List<DecodedString> Decoded { get; } = new List<DecodedString>();
    }

    /// <summary>
    /// Represents a decoded string with metadata about its source and decoding method
    /// </summary>
    public class DecodedString
    {
        /// <summary>
        /// The decoded text
        /// </summary>
        public string Decoded { get; set; } = string.Empty;

        /// <summary>
        /// The original encoded text
        /// </summary>
        public string Original { get; set; } = string.Empty;

        /// <summary>
        /// Method used for decoding (Base64, Hex, Decimal, etc.)
        /// </summary>
        public string DecodingMethod { get; set; } = string.Empty;

        /// <summary>
        /// Confidence level for this decoding (0-100)
        /// </summary>
        public int Confidence { get; set; } = 0;

        /// <summary>
        /// Recursion depth of this decoded string (0 = first level)
        /// </summary>
        public int RecursionDepth { get; set; } = 0;

        /// <summary>
        /// Chain of decoding methods used (e.g., "Hex -> Base64")
        /// </summary>
        public string DecodingChain { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides methods for detecting and decoding obfuscated content
    /// </summary>
    public static class ObfuscationHelper
    {
        // Common patterns to match potentially encoded content
        // Improved Base64Pattern with more relaxed requirements to catch PowerShell encoded commands
        private static readonly Regex Base64Pattern = new Regex(@"\b[A-Za-z0-9+/]{16,}={0,3}\b", RegexOptions.Compiled);
        private static readonly Regex Base64WordPattern = new Regex(@"\b[A-Za-z0-9+/]{8,}={0,3}\b", RegexOptions.Compiled);
        private static readonly Regex HexPattern = new Regex(@"\b(?:0x)?([0-9A-Fa-f]{2}[ ,-]?){8,}\b", RegexOptions.Compiled);
        private static readonly Regex DecimalPattern = new Regex(@"\b(?:\d{1,3}[ ,]){8,}\d{1,3}\b", RegexOptions.Compiled);
        
        // Additional pattern for PowerShell encoding detection
        private static readonly Regex PowerShellBase64Pattern = new Regex(@"-[eE](?:nc(?:odedcommand)?)?\s*[\x22\x27`]*([A-Za-z0-9+/=]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Character sets for validation
        private static readonly HashSet<char> TextChars = new HashSet<char>(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?\"'()[]{}-_+=*&^%$#@\t\r\n/\\|<>~`");

        /// <summary>
        /// Maximum recursion depth for decoding operations
        /// </summary>
        private const int MaxRecursionDepth = 10;

        /// <summary>
        /// Set of already processed strings to avoid duplicate decoding
        /// </summary>
        private static HashSet<string> _processedStrings = new HashSet<string>();

        /// <summary>
        /// Process clipboard content to extract different target types for rule matching
        /// </summary>
        /// <param name="text">Raw clipboard text</param>
        /// <param name="debugMode">Whether debug mode is enabled</param>
        /// <param name="debugCallback">Callback to log debug messages</param>
        /// <returns>ClipboardTargets object with various representations of the content</returns>
        public static ClipboardTargets ProcessClipboardContent(string text, bool debugMode = false, Action<string>? debugCallback = null)
        {
            ClipboardTargets targets = new ClipboardTargets
            {
                Raw = text,
                Normalized = NormalizeText(text)
            };

            // Add raw and normalized strings to the All collection
            targets.All.Add(targets.Raw);
            targets.All.Add(targets.Normalized);
            
            // Add normalized text to Decoded collection so it appears in decoded strings display
            targets.Decoded.Add(new DecodedString
            {
                Decoded = targets.Normalized,
                Original = targets.Raw,
                DecodingMethod = "Normalized",
                Confidence = 100,
                RecursionDepth = 0,
                DecodingChain = "Normalized"
            });
            
            // Reset the processed strings tracker for each new clipboard content
            _processedStrings.Clear();

            if (debugMode && debugCallback != null)
            {
                debugCallback($"Starting clipboard processing. Text length: {text.Length}");
                debugCallback($"Normalized text: {targets.Normalized.Substring(0, Math.Min(50, targets.Normalized.Length))}...");
            }

            // Check for PowerShell encoded commands first (specific detection)
            var psBase64Matches = PowerShellBase64Pattern.Matches(text);
            if (debugMode && debugCallback != null && psBase64Matches.Count > 0)
            {
                debugCallback($"Found {psBase64Matches.Count} PowerShell encoded command pattern matches");
            }

            foreach (Match match in psBase64Matches)
            {
                // Group 1 contains the Base64 string after the -e parameter
                if (match.Groups.Count > 1)
                {
                    string encoded = match.Groups[1].Value.Trim();
                    if (debugMode && debugCallback != null)
                    {
                        debugCallback($"Found PowerShell encoded command: {encoded.Substring(0, Math.Min(20, encoded.Length))}...");
                    }
                    
                    string decoded = TryDecodeBase64(encoded);
                    bool isValidText = !string.IsNullOrEmpty(decoded) && IsLikelyText(decoded);
                    
                    if (debugMode && debugCallback != null)
                    {
                        if (!string.IsNullOrEmpty(decoded))
                        {
                            debugCallback($"PowerShell Base64 decoded result: '{decoded.Substring(0, Math.Min(30, decoded.Length))}...'");
                            debugCallback($"IsValidText: {isValidText}");
                        }
                        else
                        {
                            debugCallback("PowerShell Base64 decoding failed.");
                        }
                    }
                    
                    if (isValidText)
                    {
                        // Create the decoding chain for this level
                        string decodingMethod = "PowerShell Base64";
                        
                        if (debugMode && debugCallback != null)
                        {
                            debugCallback($"✓ Valid PowerShell Base64 detected");
                        }
                        
                        // Add to the appropriate collections
                        targets.Base64.Add(decoded);
                        targets.All.Add(decoded);
                        
                        targets.Decoded.Add(new DecodedString
                        {
                            Decoded = decoded,
                            Original = encoded,
                            DecodingMethod = decodingMethod,
                            Confidence = CalculateConfidence(decoded, encoded),
                            RecursionDepth = 0,
                            DecodingChain = decodingMethod
                        });
                        
                        // Recursively process the decoded string
                        ProcessStringRecursively(decoded, targets, 1, decodingMethod, encoded, debugMode, debugCallback);
                    }
                }
            }

            // Process the text recursively, starting at depth 0
            ProcessStringRecursively(text, targets, 0, "", "", debugMode, debugCallback);

            if (debugMode && debugCallback != null)
            {
                debugCallback($"Found {targets.Decoded.Count} total decoded strings");
                debugCallback($"Base64: {targets.Base64.Count}, Hex: {targets.Hex.Count}, Decimal: {targets.Decimal.Count}");
                
                // Print first 3 decoded strings if available
                var sample = targets.Decoded.Take(3).ToList();
                foreach (var decoded in sample)
                {
                    string decodedSample = decoded.Decoded.Length > 50 
                        ? decoded.Decoded.Substring(0, 50) + "..." 
                        : decoded.Decoded;
                    debugCallback($"[{decoded.DecodingMethod}] {decodedSample}");
                }
            }

            return targets;
        }

        /// <summary>
        /// Recursively process a string to find and decode encoded content
        /// </summary>
        /// <param name="text">Text to process</param>
        /// <param name="targets">Clipboard targets to populate</param>
        /// <param name="depth">Current recursion depth</param>
        /// <param name="prefix">Prefix for decoded chain description</param>
        /// <param name="originalText">The original text that led to this decoding</param>
        /// <param name="debugMode">Whether debug mode is enabled</param>
        /// <param name="debugCallback">Callback to log debug messages</param>
        private static void ProcessStringRecursively(string text, ClipboardTargets targets, int depth, string prefix, string originalText, bool debugMode = false, Action<string>? debugCallback = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            if (debugMode && debugCallback != null)
            {
                debugCallback($"Processing string at depth {depth}, length {text.Length}");
                if (text.Length > 0)
                {
                    debugCallback($"First chars: {text.Substring(0, Math.Min(20, text.Length))}...");
                }
            }

            // Check if we've reached the maximum recursion depth
            if (depth >= MaxRecursionDepth)
            {
                if (debugMode && debugCallback != null)
                {
                    debugCallback("Max recursion depth reached, stopping");
                }
                return;
            }

            // Skip if this string has already been processed (to avoid cycles)
            if (_processedStrings.Contains(text))
            {
                if (debugMode && debugCallback != null)
                {
                    debugCallback("String already processed, skipping");
                }
                return;
            }

            // Add to processed strings set
            _processedStrings.Add(text);

            // Original text at depth 0 is the text itself
            string effectiveOriginal = depth == 0 ? text : originalText;

            // Extract and process potential Base64 encoded strings
            var base64Matches = Base64Pattern.Matches(text);
            if (debugMode && debugCallback != null)
            {
                debugCallback($"Found {base64Matches.Count} potential Base64 matches");
            }
            
            foreach (Match match in base64Matches)
            {
                string encoded = match.Value;
                if (debugMode && debugCallback != null)
                {
                    debugCallback($"Attempting Base64 decode: {encoded.Substring(0, Math.Min(20, encoded.Length))}...");
                }
                
                string decoded = TryDecodeBase64(encoded);
                bool isValidText = !string.IsNullOrEmpty(decoded) && IsLikelyText(decoded) && decoded.Length >= 4;
                bool isDifferent = IsDecodedTextDifferent(decoded, text);
                
                if (debugMode && debugCallback != null)
                {
                    if (!string.IsNullOrEmpty(decoded))
                    {
                        debugCallback($"Base64 decoded result: '{decoded.Substring(0, Math.Min(30, decoded.Length))}...'");
                        debugCallback($"IsValidText: {isValidText}, IsDifferent: {isDifferent}");
                    }
                    else
                    {
                        debugCallback("Base64 decoding failed.");
                    }
                }
                
                if (isValidText && isDifferent)
                {
                    // Create the decoding chain for this level
                    string decodingMethod = "Base64";
                    string decodingChain = string.IsNullOrEmpty(prefix) ? decodingMethod : $"{prefix} -> {decodingMethod}";
                    
                    if (debugMode && debugCallback != null)
                    {
                        debugCallback($"✓ Valid Base64 detected: {decodingChain}");
                    }
                    
                    // Add to the appropriate collections
                    targets.Base64.Add(decoded);
                    targets.All.Add(decoded);
                    
                    targets.Decoded.Add(new DecodedString
                    {
                        Decoded = decoded,
                        Original = encoded,
                        DecodingMethod = decodingMethod,
                        Confidence = CalculateConfidence(decoded, encoded),
                        RecursionDepth = depth,
                        DecodingChain = decodingChain
                    });
                    
                    // Recursively process the decoded string
                    ProcessStringRecursively(decoded, targets, depth + 1, decodingChain, encoded, debugMode, debugCallback);
                }
            }
            
            // Also try to match shorter Base64 strings that might be words or short commands
            var base64WordMatches = Base64WordPattern.Matches(text);
            // Filter out any matches that were already caught by the longer pattern
            var uniqueWordMatches = base64WordMatches
                .Cast<Match>()
                .Where(m => base64Matches.Cast<Match>().All(lm => !lm.Value.Contains(m.Value)))
                .ToList();
                
            if (debugMode && debugCallback != null && uniqueWordMatches.Count > 0)
            {
                debugCallback($"Found {uniqueWordMatches.Count} potential shorter Base64 matches");
            }
            
            foreach (Match match in uniqueWordMatches)
            {
                string encoded = match.Value;
                // Skip very short matches as they're likely to be false positives
                if (encoded.Length < 12) continue;
                
                if (debugMode && debugCallback != null)
                {
                    debugCallback($"Attempting short Base64 decode: {encoded}");
                }
                
                string decoded = TryDecodeBase64(encoded);
                bool isValidText = !string.IsNullOrEmpty(decoded) && IsLikelyText(decoded) && decoded.Length >= 3;
                bool isDifferent = IsDecodedTextDifferent(decoded, text);
                
                if (debugMode && debugCallback != null)
                {
                    if (!string.IsNullOrEmpty(decoded))
                    {
                        debugCallback($"Short Base64 decoded result: '{decoded}'");
                        debugCallback($"IsValidText: {isValidText}, IsDifferent: {isDifferent}");
                    }
                }
                
                if (isValidText && isDifferent && decoded.Length > 2)
                {
                    // Create the decoding chain for this level
                    string decodingMethod = "Base64";
                    string decodingChain = string.IsNullOrEmpty(prefix) ? decodingMethod : $"{prefix} -> {decodingMethod}";
                    
                    if (debugMode && debugCallback != null)
                    {
                        debugCallback($"✓ Valid short Base64 detected: {decodingChain} - '{decoded}'");
                    }
                    
                    // Add to the appropriate collections
                    targets.Base64.Add(decoded);
                    targets.All.Add(decoded);
                    
                    targets.Decoded.Add(new DecodedString
                    {
                        Decoded = decoded,
                        Original = encoded,
                        DecodingMethod = decodingMethod,
                        Confidence = CalculateConfidence(decoded, encoded),
                        RecursionDepth = depth,
                        DecodingChain = decodingChain
                    });
                    
                    // Recursively process the decoded string
                    ProcessStringRecursively(decoded, targets, depth + 1, decodingChain, encoded, debugMode, debugCallback);
                }
            }

            // Extract and process potential Hex encoded strings
            var hexMatches = HexPattern.Matches(text);
            if (debugMode && debugCallback != null)
            {
                debugCallback($"Found {hexMatches.Count} potential Hex matches");
            }
            
            foreach (Match match in hexMatches)
            {
                string encoded = match.Value;
                if (debugMode && debugCallback != null)
                {
                    debugCallback($"Attempting Hex decode: {encoded.Substring(0, Math.Min(20, encoded.Length))}...");
                }
                
                string decoded = TryDecodeHex(encoded);
                bool isValidText = !string.IsNullOrEmpty(decoded) && IsLikelyText(decoded) && decoded.Length >= 4;
                bool isDifferent = IsDecodedTextDifferent(decoded, text);
                
                if (debugMode && debugCallback != null)
                {
                    if (!string.IsNullOrEmpty(decoded))
                    {
                        debugCallback($"Hex decoded result: '{decoded.Substring(0, Math.Min(30, decoded.Length))}...'");
                        debugCallback($"IsValidText: {isValidText}, IsDifferent: {isDifferent}");
                    }
                    else
                    {
                        debugCallback("Hex decoding failed.");
                    }
                }
                
                if (isValidText && isDifferent)
                {
                    // Create the decoding chain for this level
                    string decodingMethod = "Hex";
                    string decodingChain = string.IsNullOrEmpty(prefix) ? decodingMethod : $"{prefix} -> {decodingMethod}";
                    
                    if (debugMode && debugCallback != null)
                    {
                        debugCallback($"✓ Valid Hex detected: {decodingChain}");
                    }
                    
                    // Add to the appropriate collections
                    targets.Hex.Add(decoded);
                    targets.All.Add(decoded);
                    
                    targets.Decoded.Add(new DecodedString
                    {
                        Decoded = decoded,
                        Original = effectiveOriginal,
                        DecodingMethod = decodingMethod,
                        Confidence = CalculateConfidence(decoded, encoded),
                        RecursionDepth = depth,
                        DecodingChain = decodingChain
                    });
                    
                    // Recursively process the decoded string
                    ProcessStringRecursively(decoded, targets, depth + 1, decodingChain, effectiveOriginal, debugMode, debugCallback);
                }
            }

            // Extract and process potential Decimal encoded strings
            var decimalMatches = DecimalPattern.Matches(text);
            if (debugMode && debugCallback != null)
            {
                debugCallback($"Found {decimalMatches.Count} potential Decimal matches");
            }
            
            foreach (Match match in decimalMatches)
            {
                string encoded = match.Value;
                if (debugMode && debugCallback != null)
                {
                    debugCallback($"Attempting Decimal decode: {encoded.Substring(0, Math.Min(20, encoded.Length))}...");
                }
                
                string decoded = TryDecodeDecimal(encoded);
                bool isValidText = !string.IsNullOrEmpty(decoded) && IsLikelyText(decoded) && decoded.Length >= 4;
                bool isDifferent = IsDecodedTextDifferent(decoded, text);
                
                if (debugMode && debugCallback != null)
                {
                    if (!string.IsNullOrEmpty(decoded))
                    {
                        debugCallback($"Decimal decoded result: '{decoded.Substring(0, Math.Min(30, decoded.Length))}...'");
                        debugCallback($"IsValidText: {isValidText}, IsDifferent: {isDifferent}");
                    }
                    else
                    {
                        debugCallback("Decimal decoding failed.");
                    }
                }
                
                if (isValidText && isDifferent)
                {
                    // Create the decoding chain for this level
                    string decodingMethod = "Decimal";
                    string decodingChain = string.IsNullOrEmpty(prefix) ? decodingMethod : $"{prefix} -> {decodingMethod}";
                    
                    if (debugMode && debugCallback != null)
                    {
                        debugCallback($"✓ Valid Decimal detected: {decodingChain}");
                    }
                    
                    // Add to the appropriate collections
                    targets.Decimal.Add(decoded);
                    targets.All.Add(decoded);
                    
                    targets.Decoded.Add(new DecodedString
                    {
                        Decoded = decoded,
                        Original = effectiveOriginal,
                        DecodingMethod = decodingMethod,
                        Confidence = CalculateConfidence(decoded, encoded),
                        RecursionDepth = depth,
                        DecodingChain = decodingChain
                    });
                    
                    // Recursively process the decoded string
                    ProcessStringRecursively(decoded, targets, depth + 1, decodingChain, effectiveOriginal, debugMode, debugCallback);
                }
            }
        }

        /// <summary>
        /// Normalize text by removing quotes, extra spaces, and special characters
        /// </summary>
        private static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            
            // Remove quotes and normalize spaces, but preserve case
            string normalized = Regex.Replace(text, @"[""`^'']", "");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            
            return normalized;
        }

        /// <summary>
        /// Try to decode a Base64 string
        /// </summary>
        /// <param name="encoded">The encoded Base64 string</param>
        /// <returns>The decoded string or empty if decoding fails</returns>
        private static string TryDecodeBase64(string encoded)
        {
            try
            {
                // Remove any whitespace which might be in the encoded string
                string cleaned = Regex.Replace(encoded, @"\s", "");
                
                // Ensure proper padding
                int padNeeded = cleaned.Length % 4;
                if (padNeeded > 0)
                {
                    cleaned = cleaned.PadRight(cleaned.Length + (4 - padNeeded), '=');
                }
                
                byte[] data = Convert.FromBase64String(cleaned);
                
                // Try multiple encodings - PowerShell typically uses UTF-16LE
                List<Encoding> encodingsToTry = new List<Encoding>
                {
                    Encoding.UTF8,           // Standard text
                    Encoding.Unicode,        // UTF-16LE (PowerShell default for -EncodedCommand)
                    Encoding.UTF32,          // UTF-32
                    Encoding.ASCII,          // ASCII
                    Encoding.BigEndianUnicode // UTF-16BE
                };
                
                // Try each encoding, preferring results with valid text characters
                foreach (var encoding in encodingsToTry)
                {
                    try
                    {
                        string decoded = encoding.GetString(data);
                        
                        // Skip strings with lots of nulls (0x00) which is common in mismatched encoding
                        int nullCount = decoded.Count(c => c == '\0');
                        if (nullCount > decoded.Length / 4)
                        {
                            continue;
                        }
                        
                        // If we get a string that looks valid, return it
                        if (IsLikelyText(decoded))
                        {
                            return decoded;
                        }
                    }
                    catch
                    {
                        // Continue with next encoding if one fails
                        continue;
                    }
                }
                
                // Default to UTF-8 if none of the encodings produce good output
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Try again with default padding
                try 
                {
                    string cleaned = Regex.Replace(encoded, @"\s", "");
                    // Add padding if missing
                    while (cleaned.Length % 4 != 0)
                    {
                        cleaned += "=";
                    }
                    byte[] data = Convert.FromBase64String(cleaned);
                    
                    // Try multiple encodings
                    List<Encoding> encodingsToTry = new List<Encoding>
                    {
                        Encoding.Unicode,    // UTF-16LE (PowerShell default)
                        Encoding.UTF8,       // Standard text
                        Encoding.ASCII,      // ASCII
                        Encoding.UTF32,      // UTF-32
                        Encoding.BigEndianUnicode // UTF-16BE
                    };
                    
                    foreach (var encoding in encodingsToTry)
                    {
                        try
                        {
                            string decoded = encoding.GetString(data);
                            int nullCount = decoded.Count(c => c == '\0');
                            if (nullCount > decoded.Length / 4)
                            {
                                continue;
                            }
                            
                            if (IsLikelyText(decoded))
                            {
                                return decoded;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    
                    // Default to UTF-16LE if none of the encodings produce good output
                    return Encoding.Unicode.GetString(data);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Try to decode a Hex encoded string
        /// </summary>
        private static string TryDecodeHex(string encoded)
        {
            try
            {
                // Clean up hex string - remove 0x prefixes, spaces, commas
                string cleaned = Regex.Replace(encoded, @"(0x|[\s,-])", "");
                
                // Convert pairs of hex chars to bytes
                List<byte> bytes = new List<byte>();
                for (int i = 0; i < cleaned.Length; i += 2)
                {
                    if (i + 1 >= cleaned.Length) break;
                    string hex = cleaned.Substring(i, 2);
                    bytes.Add(Convert.ToByte(hex, 16));
                }
                
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Try to decode a Decimal encoded string (ASCII/Unicode values)
        /// </summary>
        private static string TryDecodeDecimal(string encoded)
        {
            try
            {
                // Split by space or comma
                string[] parts = Regex.Split(encoded, @"[ ,]+");
                List<byte> bytes = new List<byte>();
                
                foreach (string part in parts)
                {
                    if (int.TryParse(part, out int value) && value >= 0 && value <= 255)
                    {
                        bytes.Add((byte)value);
                    }
                }
                
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Check if a string is likely to be actual text rather than binary garbage
        /// </summary>
        private static bool IsLikelyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Remove null characters which might be present in UTF-16 encoded strings
            // when decoded with the wrong encoding
            string cleanText = text.Replace("\0", "");
            if (string.IsNullOrEmpty(cleanText)) return false;
            
            // Count printable characters
            int textCharCount = cleanText.Count(c => TextChars.Contains(c));
            double ratio = (double)textCharCount / cleanText.Length;
            
            // At least 70% of chars should be normal text characters (lowered from 80%)
            // to accommodate scripts with special characters
            return ratio >= 0.7;
        }

        /// <summary>
        /// Check if the decoded text is substantially different from the original
        /// to avoid false positives
        /// </summary>
        private static bool IsDecodedTextDifferent(string decoded, string original)
        {
            // Simple case: if original doesn't contain the decoded content
            if (!original.Contains(decoded)) return true;
            
            // More complex case: compare character frequency distributions
            var originalCounts = CountCharFrequency(original);
            var decodedCounts = CountCharFrequency(decoded);
            
            double difference = CalculateDistributionDifference(originalCounts, decodedCounts);
            return difference > 0.4; // Threshold determined by testing
        }

        /// <summary>
        /// Count character frequencies in a string
        /// </summary>
        private static Dictionary<char, double> CountCharFrequency(string text)
        {
            Dictionary<char, int> counts = new Dictionary<char, int>();
            foreach (char c in text)
            {
                if (!counts.ContainsKey(c)) counts[c] = 0;
                counts[c]++;
            }
            
            // Convert to percentages
            Dictionary<char, double> frequencies = new Dictionary<char, double>();
            foreach (var kvp in counts)
            {
                frequencies[kvp.Key] = (double)kvp.Value / text.Length;
            }
            
            return frequencies;
        }

        /// <summary>
        /// Calculate the difference between two character distributions
        /// </summary>
        private static double CalculateDistributionDifference(
            Dictionary<char, double> dist1, 
            Dictionary<char, double> dist2)
        {
            HashSet<char> allChars = new HashSet<char>(dist1.Keys.Concat(dist2.Keys));
            double totalDiff = 0;
            
            foreach (char c in allChars)
            {
                double freq1 = dist1.ContainsKey(c) ? dist1[c] : 0;
                double freq2 = dist2.ContainsKey(c) ? dist2[c] : 0;
                totalDiff += Math.Abs(freq1 - freq2);
            }
            
            return totalDiff / 2.0; // Normalize to 0-1 range
        }

        /// <summary>
        /// Calculate confidence score for a decoded string
        /// </summary>
        private static int CalculateConfidence(string decoded, string original)
        {
            // Base confidence
            int confidence = 50;
            
            // Adjust based on similarity to original
            if (!original.Contains(decoded)) confidence += 10;
            
            // Adjust based on text characteristics
            if (decoded.Contains(" ")) confidence += 15; // Has spaces (likely real text)
            if (Regex.IsMatch(decoded, @"[.!?]\s")) confidence += 10; // Has sentence endings
            
            // Adjust based on entropy/randomness of decoded text
            double entropy = CalculateEntropy(decoded);
            if (entropy > 3.0 && entropy < 4.5) confidence += 10; // Typical range for English text
            
            // Cap at 0-100
            return Math.Min(100, Math.Max(0, confidence));
        }

        /// <summary>
        /// Calculate Shannon entropy of a string
        /// </summary>
        private static double CalculateEntropy(string text)
        {
            Dictionary<char, int> counts = new Dictionary<char, int>();
            foreach (char c in text)
            {
                if (!counts.ContainsKey(c)) counts[c] = 0;
                counts[c]++;
            }
            
            double entropy = 0;
            double textLength = text.Length;
            
            foreach (var count in counts.Values)
            {
                double probability = count / textLength;
                entropy -= probability * Math.Log(probability, 2);
            }
            
            return entropy;
        }
    }
}