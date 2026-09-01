namespace BeastSoccer.Data
{
    public enum GameMode { Regular, Defending }
    public enum CharacterType { Leo, Goro, Volt, Generic }
    public enum TeamSide { Home, Away }
    public enum FieldRole { Outfield, Goalkeeper }
    public enum TacticalRole { Anchor, Presser, Rover, Goalkeeper }
    public enum MatchPhase { PreMatch, Kickoff, Playing, SetPiece, GoalScored, StopMade, HalfTime, Paused, MatchOver }
    public enum Possession { Home, Away, Loose }
    public enum KickType { None, Shot, Pass, Through, Lob }
    public enum SetPieceType { None, ThrowIn, GoalKick, Corner }
}
