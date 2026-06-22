using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.GetPaymentByOrderCode
{
    public class GetPaymentByLoginQuery : IRequest<Result<PaymentResponse>>
    {
        public long OrderCode { get; set; }

    }
}
