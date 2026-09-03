namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class AmUserInfoDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public bool IsActive { get; set; }

    public double Score { get; set; }

    public string Email { get; set; } = string.Empty;

    public int? Age { get; set; }
}
