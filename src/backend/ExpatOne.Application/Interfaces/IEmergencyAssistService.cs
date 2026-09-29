using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IEmergencyAssistService
{
    Task<EmergencyAssistResponseDto> AssistAsync(EmergencyAssistRequestDto request);
}
