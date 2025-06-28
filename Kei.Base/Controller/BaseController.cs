using Kei.Base.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Controller
{
    public abstract class BaseController : ControllerBase
    {
        protected IActionResult Success() => Ok(OperationResult.Ok());
        protected IActionResult Success(string message) => Ok(OperationResult.Ok(message));
        protected IActionResult Success<T>(T data, string? message = null) =>
            Ok(OperationResult<T>.Ok(data, message));
        protected IActionResult Error(string message) =>
            BadRequest(OperationResult.Fail(message));
        protected IActionResult Error<T>(string message) =>
            BadRequest(OperationResult<T>.Fail(message));
    }
}
