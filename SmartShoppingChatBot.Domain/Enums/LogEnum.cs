using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Enums
{
    public enum ActionLogEnums
    {
        Create = 1,
        Update = 2,
        Delete = 3,
        Login = 4,
        Logout = 5,
        PasswordChange = 6,
        PasswordReset = 7,
        PermissionChange = 8,
        RoleChange = 9,
        DataExport = 10,
        DataImport = 11
    }
    public enum SeverityLogEnums
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
    public enum StatusLogEnums
    {
        Success = 1,
        Failure = 2,       
    }

}
