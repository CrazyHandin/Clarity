using System;
using System.Linq;
using System.Windows;
namespace ClearSpace {
public static class AdministratorPrompt {
 public static bool ShouldOffer(CleanResult result,bool administrator){return !administrator&&result!=null&&!result.Cancelled&&(result.Failed>0||result.PermissionDenied>0);}
 public static bool Show(Window owner,CleanResult result){return UiText.Show(owner,"部分项目清理失败或没有删除权限。是否以管理员身份重新启动？\n\n管理员权限可能帮助处理权限限制；占用文件和安全软件拦截仍可能导致失败。重启后需手动重新扫描，不会自动继续清理。",App.DisplayName,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
}
}
