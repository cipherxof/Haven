using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HavenStudio;

public sealed class CryptoWindowViewModel : INotifyPropertyChanged
{
    private readonly CryptoService _cryptoService = new();
    private string _keyText;
    private string _statusText = string.Empty;

    public CryptoWindowViewModel(string filePath, string defaultKey)
    {
        FilePath = filePath;
        IsFolder = Directory.Exists(filePath);
        FileName = IsFolder ? new DirectoryInfo(filePath).Name : Path.GetFileName(filePath);
        _keyText = defaultKey;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public bool IsFolder { get; }
    public string PathTypeLabel => IsFolder ? "Folder:" : "File:";

    public string KeyText
    {
        get => _keyText;
        set
        {
            if (_keyText == value)
            {
                return;
            }

            _keyText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public void RunEncrypt(string? outputFolderPath = null)
    {
        try
        {
            if (IsFolder)
            {
                if (string.IsNullOrWhiteSpace(outputFolderPath))
                {
                    StatusText = "No output folder selected.";
                    return;
                }
                _cryptoService.EncryptFolder(FilePath, outputFolderPath, KeyText);
                StatusText = $"Encrypted files written to {outputFolderPath}";
            }
            else
            {
                _cryptoService.Encrypt(FilePath, KeyText);
                StatusText = "Encrypted file written next to source (.enc).";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Encrypt failed: {ex.Message}";
        }
    }

    public void RunDecrypt(string? outputFolderPath = null)
    {
        try
        {
            if (IsFolder)
            {
                if (string.IsNullOrWhiteSpace(outputFolderPath))
                {
                    StatusText = "No output folder selected.";
                    return;
                }
                _cryptoService.DecryptFolder(FilePath, outputFolderPath, KeyText);
                StatusText = $"Decrypted files written to {outputFolderPath}";
            }
            else
            {
                _cryptoService.Decrypt(FilePath, KeyText);
                StatusText = "Decrypted file written next to source (.dec).";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Decrypt failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
