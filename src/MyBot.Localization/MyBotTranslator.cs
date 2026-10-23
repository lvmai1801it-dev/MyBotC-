using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MyBot.Localization;

/// <summary>
/// Kiểu dữ liệu cấp vùng (xem <see cref="LanguageRegion"/>) dùng làm khoá tìm kiếm
/// chuỗi dịch trong từ điển i18n (tương đương $(langcode) và $(langregion) của AutoIt).
/// </summary>
public readonly record struct LanguageRegion(string Code, string? Region = null)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Region) ? Code : $"{Code}-{Region}";
}

/// <summary>
/// Cấu trúc chuỗi dịch (sau khi nội suy tham số {0}, {1}…), kèm vùng để ghi đè
/// theo ngôn ngữ — tương đương kiểu dữ liệu `$sLangString` của AutoIt.
/// </summary>
public sealed record TranslatedString(string Value, string? Region = null);

/// <summary>
/// Điểm vào tra cứu i18n cho toàn bộ GUI — port của
/// <c>GetTranslatedFileIni(section, key, default)</c> trong AutoIt.
///
/// Quy ước giống hệt bản gốc:
/// <list type="bullet">
///   <item><c>T(section, key, default)</c> tra từ điển ngôn ngữ đang chọn; thiếu khoá
///         thì trả về <paramref name="default"/>, ngoại trừ <paramref name="default"/> = -1
///         (nghĩa là "bắt buộc phải có") — khi đó tra English.ini, vẫn thiếu thì
///         trả về <c>"-3"</c> như AutoIt.</item>
///   <item>Nếu file ngôn ngữ thiếu toàn bộ mục <paramref name="section"/> thì
///         <c>StringContainsKey</c> trả về false và <c>GetStringWithRegion</c> chỉ dùng
///         English.</item>
///   <item>Đối tượng tự chữa lỗi thiếu khoá bằng cách ghi giá trị tiếng Anh vào
///         English.ini khi được cho phép (mặc định <c>FixIncompleteLanguageFile=true</c>).</item>
/// </list>
/// </summary>
public interface IMyBotTranslator
{
    /// <summary>Tra cứu chuỗi dịch; xem mô tả quy ước ở lớp triển khai.</summary>
    string T(string section, string key, string? defaultValue);

    /// <summary>Tra cứu chuỗi dịch kèm vùng ghi đè theo ngôn ngữ.</summary>
    TranslatedString GetStringWithRegion(string section, string key, string? defaultValue);

    /// <summary>true nếu mục <paramref name="section"/> có mặt trong file ngôn ngữ đang chọn.</summary>
    bool StringContainsKey(string section);

    /// <summary>Tên file ngôn ngữ đang dùng (ví dụ "English", "Vietnamese").</summary>
    string LanguageName { get; }

    /// <summary>Danh sách ngôn ngữ có sẵn (tên file .ini trong thư mục Languages).</summary>
    IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>Đổi ngôn ngữ đang dùng; ném <see cref="KeyNotFoundException"/> nếu không có file.</summary>
    void SetLanguage(string languageName);

    /// <summary>true nếu chuỗi được trả về là khoá bị thiếu (-3) hoặc giá trị thô của khoá.</summary>
    bool IsKeyMissing(string section, string key);

    /// <summary>Ghi chuỗi dịch thực tế (theo khoá + vùng) — dùng cho các giá trị do bot cập nhật.</summary>
    void SetString(string section, string key, string value);

    /// <summary>Đồng bộ các cặp (section, key) của khoá đã cho từ English.ini sang ngôn ngữ hiện tại.</summary>
    void CopyLanguageSection(string section);
}

/// <summary>
/// Triển khai <see cref="IMyBotTranslator"/> đọc trực tiếp các file
/// <c>Languages/*.ini</c> (định dạng INI chuẩn, tiêu đề mục <c>[Section]</c>).
/// </summary>
public sealed class IniFileTranslator : IMyBotTranslator
{
    private const string MissingString = "-3";

    private readonly string _languagesDirectory;
    private readonly bool _fixIncompleteLanguageFile;

    private Dictionary<string, Dictionary<string, string>>? _english;
    private Dictionary<string, Dictionary<string, string>>? _current;

    /// <summary>
    /// Khởi tạo bộ dịch từ thư mục chứa các file <c>*.ini</c> ngôn ngữ.
    /// </summary>
    /// <param name="languagesDirectory">Thư mục Languages (khi không tồn tại, bộ dịch trả thẳng default).</param>
    /// <param name="languageName">Ngôn ngữ ban đầu; mặc định "English".</param>
    /// <param name="fixIncompleteLanguageFile">
    /// Cho phép tự ghi khoá tiếng Anh còn thiếu vào English.ini (đúng hành vi AutoIt).
    /// </param>
    public IniFileTranslator(string languagesDirectory, string languageName = "English", bool fixIncompleteLanguageFile = true)
    {
        _languagesDirectory = languagesDirectory;
        _fixIncompleteLanguageFile = fixIncompleteLanguageFile;

        // Nạp English.ini nếu có; ngược lại dùng từ điển nhúng sẵn (fallback khi
        // thư mục Languages chưa được copy ra output).
        if (Directory.Exists(languagesDirectory)
            && File.Exists(Path.Combine(languagesDirectory, "English.ini")))
        {
            _english = IniFileLoader.LoadAll(Path.Combine(languagesDirectory, "English.ini"));
        }
        else
        {
            _english = DeepCopy(EnglishStrings.Sections);
        }

        SetLanguage(languageName);
    }

    private static Dictionary<string, Dictionary<string, string>> DeepCopy(
        IReadOnlyDictionary<string, Dictionary<string, string>> source)
    {
        var copy = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach ((string section, Dictionary<string, string> values) in source)
        {
            copy[section] = new Dictionary<string, string>(values, StringComparer.Ordinal);
        }

        return copy;
    }

    public string LanguageName { get; private set; } = "English";

    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            var list = new List<string>();
            if (!Directory.Exists(_languagesDirectory))
            {
                return list;
            }

            foreach (string file in Directory.GetFiles(_languagesDirectory, "*.ini"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!string.Equals(name, LanguageName, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(name);
                }
            }

            list.Insert(0, LanguageName);
            return list;
        }
    }

    public void SetLanguage(string languageName)
    {
        LanguageName = languageName;

        if (string.Equals(languageName, "English", StringComparison.OrdinalIgnoreCase))
        {
            _current = _english;
            return;
        }

        string path = Path.Combine(_languagesDirectory, $"{languageName}.ini");
        _current = File.Exists(path)
            ? IniFileLoader.LoadAll(path)
            : throw new KeyNotFoundException($"Không tìm thấy file ngôn ngữ: {path}");
    }

    /// <inheritdoc />
    public string T(string section, string key, string? defaultValue)
    {
        if (_current is null || string.IsNullOrEmpty(section))
        {
            return defaultValue ?? string.Empty;
        }

        // Các khoá đặc biệt của AutoIt (không nằm trong file ngôn ngữ).
        if (section == "Program Languages")
        {
            return key; // dùng trực tiếp tên ngôn ngữ
        }

        if (_current.TryGetValue(section, out Dictionary<string, string>? secValues)
            && secValues.TryGetValue(key, out string? value))
        {
            return value;
        }

        // Khoá "bắt buộc phải có" (-1): chỉ tồn tại trong English.ini.
        if (string.Equals(defaultValue, "-1", StringComparison.Ordinal))
        {
            if (_english is not null
                && _english.TryGetValue(section, out Dictionary<string, string>? enSec)
                && enSec.TryGetValue(key, out string? enValue))
            {
                return enValue;
            }

            return MissingString;
        }

        return defaultValue ?? string.Empty;
    }

    /// <inheritdoc />
    public TranslatedString GetStringWithRegion(string section, string key, string? defaultValue)
    {
        if (_current is null || string.IsNullOrEmpty(section))
        {
            return new TranslatedString(defaultValue ?? string.Empty);
        }

        if (_current.TryGetValue(section, out Dictionary<string, string>? secValues))
        {
            if (secValues.TryGetValue(key, out string? value))
            {
                return new TranslatedString(value);
            }

            if (secValues.TryGetValue($"{key}({LanguageName})", out string? regional))
            {
                return new TranslatedString(regional, LanguageName);
            }
        }

        return new TranslatedString(T(section, key, defaultValue));
    }

    /// <inheritdoc />
    public bool StringContainsKey(string section)
    {
        if (_current is null)
        {
            return false;
        }

        // Section với -1 (bắt buộc) chỉ có trong English; mục trống cũng coi là thiếu.
        return _current.TryGetValue(section, out Dictionary<string, string>? values)
            && values.Count > 0;
    }

    /// <inheritdoc />
    public bool IsKeyMissing(string section, string key)
    {
        string value = T(section, key, "-1");
        return string.Equals(value, MissingString, StringComparison.Ordinal)
            || string.Equals(value, key, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void SetString(string section, string key, string value)
    {
        if (_current is null)
        {
            return;
        }

        if (!_current.TryGetValue(section, out Dictionary<string, string>? secValues))
        {
            secValues = new Dictionary<string, string>(StringComparer.Ordinal);
            _current[section] = secValues;
        }

        secValues[key] = value;
        _dirty = true;
    }

    /// <inheritdoc />
    public void CopyLanguageSection(string section)
    {
        if (_english is null || _current is null || _current == _english)
        {
            return;
        }

        if (_english.TryGetValue(section, out Dictionary<string, string>? enSec))
        {
            if (!_current.TryGetValue(section, out Dictionary<string, string>? curSec))
            {
                curSec = new Dictionary<string, string>(StringComparer.Ordinal);
                _current[section] = curSec;
            }

            foreach ((string key, string value) in enSec)
            {
                curSec.TryAdd(key, value);
            }
        }
    }

    /// <summary>true nếu có chuỗi nào được <see cref="SetString"/> hoặc tự sửa khoá thiếu.</summary>
    private bool _dirty;

    /// <summary>
    /// Ghi toàn bộ file ngôn ngữ hiện tại (kể cả các chuỗi do bot cập nhật)
    /// về thư mục Languages. Bỏ qua khi không có thay đổi.
    /// </summary>
    public void SaveToDisk()
    {
        if (!_dirty || _current is null || !Directory.Exists(_languagesDirectory))
        {
            return;
        }

        string path = Path.Combine(_languagesDirectory, $"{LanguageName}.ini");
        IniFileLoader.SaveAll(path, _current);
        _dirty = false;
    }

    /// <summary>Ghi khoá tiếng Anh còn thiếu vào English.ini (hành vi tự chữa của AutoIt).</summary>
    internal void EnsureKeyInEnglish(string section, string key, string enValue)
    {
        if (!_fixIncompleteLanguageFile || _english is null)
        {
            return;
        }

        if (!_english.TryGetValue(section, out Dictionary<string, string>? secValues))
        {
            secValues = new Dictionary<string, string>(StringComparer.Ordinal);
            _english[section] = secValues;
        }

        if (!secValues.ContainsKey(key))
        {
            secValues[key] = enValue;
            _dirty = true;
        }
    }
}

/// <summary>Bộ đọc/ghi file INI giữ nguyên thứ tự mục và khoá.</summary>
internal static class IniFileLoader
{
    public static Dictionary<string, Dictionary<string, string>> LoadAll(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string section = line[1..^1].Trim();
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                result[section] = current;
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0 || current is null)
            {
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            current[key] = value;
        }

        return result;
    }

    public static void SaveAll(string path, Dictionary<string, Dictionary<string, string>> sections)
    {
        using var writer = new StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8);
        foreach ((string section, Dictionary<string, string> values) in sections)
        {
            writer.WriteLine($"[{section}]");
            foreach ((string key, string value) in values)
            {
                writer.WriteLine($"{key}={value}");
            }

            writer.WriteLine();
        }
    }
}
