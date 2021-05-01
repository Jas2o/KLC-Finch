namespace KLC_Finch {
    public class TSSession {
        public string session_id;
        public string session_name;

        public TSSession(string session_id, string session_name) {
            this.session_id = session_id;
            this.session_name = session_name;
        }
    }
}