namespace GymSync.Server.Controllers
{
    public class Response
    {
        public int ResponseCode { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }

        public Response(int statusCode, string message, object data = null)
        {
            ResponseCode = statusCode;
            Message = message;
            Data = data;
        }
        public Response(string Status, string message)
        {
            this.Status = Status;
            Message = message;
        }
        public Response() { }
    }
}
