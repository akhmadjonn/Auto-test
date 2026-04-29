using AutoTest.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Services;

public interface IResponseService
{
    IActionResult GetResponse(BaseResponse response);
    IActionResult GetResponse<T>(BaseResponse<T> response);
}
