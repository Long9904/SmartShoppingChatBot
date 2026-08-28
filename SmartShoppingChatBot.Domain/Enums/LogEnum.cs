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
        Read = 4,
        Login = 5,
        Logout = 6,
        PasswordChange = 7,
        PasswordReset = 8,
        PermissionChange = 9,
        RoleChange = 10,
        DataExport = 11,
        DataImport = 12
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
