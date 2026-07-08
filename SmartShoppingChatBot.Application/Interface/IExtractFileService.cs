using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IExtractFileService
    {
        Task<string> ExtractDocxAsync(Stream stream);
        Task<string> ExtractTxtAsync(Stream stream);
    }
}
