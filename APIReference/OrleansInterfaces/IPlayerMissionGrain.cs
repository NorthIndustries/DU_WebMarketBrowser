namespace NQ.Interfaces
{
    public interface IPlayerMissionGrain : Orleans.IGrainWithIntegerKey
    {
        Task<MissionStats> Statistics();
        Task<MissionId> Create(MissionCreation mc);
        Task Update(MissionUpdate update);
        Task Assign(MissionAccepted ma);
        Task<MissionsActive> MyActive();
        Task<MissionHistory> History(ulong playerId);
        Task<Missions> Live();
        Task<MissionRespondents> Respondents(MissionId mid);
        Task<MissionMessages> ChatHistory(MissionId mid);
        Task Post(MissionPostMessage msg);
        Task Apply(MissionId mid);
        Task Rate(FormalMissionRating rating);
    }
}
