using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Features.Update;
using Starward.Frameworks;
using System;
using System.Threading.Tasks;


namespace Starward.Features.Setting;

public sealed partial class AboutSetting : PageBase
{


    private readonly ILogger<AboutSetting> _logger = AppConfig.GetLogger<AboutSetting>();


    public AboutSetting()
    {
        this.InitializeComponent();
    }




    /// <summary>
    /// 预览版
    /// </summary>
    public bool EnablePreviewRelease
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnablePreviewRelease = value;
            }
        }
    } = AppConfig.EnablePreviewRelease;


    /// <summary>
    /// 是最新版
    /// </summary>
    public bool IsUpdated { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 更新错误文本
    /// </summary>
    public string? UpdateErrorText { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 检查更新 - 已禁用（分支版本）
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        try
        {
            IsUpdated = false;
            UpdateErrorText = "此为分支版本，已禁用自动更新功能";
            await Task.CompletedTask;
            
            // 原始更新检查代码已禁用
            //var release = await AppConfig.GetService<UpdateService>().CheckUpdateAsync(true);
            //if (release != null)
            //{
            //    new UpdateWindow { NewVersion = release }.Activate();
            //}
            //else
            //{
            //    IsUpdated = true;
            //}
        }
        catch (Exception ex)
        {
            UpdateErrorText = ex.Message;
            _logger.LogError(ex, "Check update");
        }
    }




}
