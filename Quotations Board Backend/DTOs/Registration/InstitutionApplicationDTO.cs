using DocumentFormat.OpenXml.Office.CoverPageProps;

public class InstitutionApplicationDTO
{
    public string Id { get; set; } = null!;
    public string InstitutionName { get; set; } = null!;
    public string ApplicationStatus { get; set; } = null!;
    public string AdministratorEmail { get; set; } = null!;
    public string AdministratorPhoneNumber { get; set; } = null!;
    public string AdministratorName { get; set; } = null!;
    public string InstitutionAddress { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string InstitutionType { get; set; } = null!;
    public string InstitutionEmail { get; set; } = null!;
    public string InstitutionId { get; set; } = null!;
    public Boolean IsActive { get; set; }
    public Boolean IsAPIAccessEnabled { get; set; } = false;
    public Boolean IsWidgetAccessEnabled { get; set; } = false;

}