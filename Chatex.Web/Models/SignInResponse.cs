namespace Chatex.Web.Models
{
    public class SignInResponse
    {
        /// <summary>
        /// UUID of tha authenticated user
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// The JWT token
        /// </summary>
        public required string Token { get; set; }
    }
}
