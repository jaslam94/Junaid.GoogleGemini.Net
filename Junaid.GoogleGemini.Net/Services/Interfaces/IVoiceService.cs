using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Wraps Google's Voices API (<c>/v1beta/voices</c>): browse the voice library, make a custom voice, and
    /// delete it. A voice's <see cref="Voice.Id"/> goes in <c>GeminiRequestOptions.VoiceName</c> to speak with it.
    /// Only the Gemini 3.8 TTS models use custom voices.
    ///
    /// Creating a voice runs a model, so it uses tokens and counts against your spending cap. A designed voice
    /// used 662 tokens when we tried it, well under a cent. That usage does not feed cost governance.
    /// Listing, getting and deleting use no tokens. Stored voices expire after a year, and a project can keep
    /// 200 of them.
    /// </summary>
    public interface IVoiceService
    {
        /// <summary>
        /// Gets one page of voices: your stored custom voices first, then the prebuilt voices that match the
        /// filters. Use <see cref="ListAllAsync"/> to read every page.
        /// </summary>
        Task<VoiceList> ListAsync(VoiceListOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads every page and yields each voice. With no filters this is more than a thousand prebuilt
        /// voices, so filter first or stop early.
        /// </summary>
        IAsyncEnumerable<Voice> ListAllAsync(VoiceListOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one of your stored custom voices by id, including its preview clip. A prebuilt voice returns
        /// HTTP 404 here (live-verified): find those with <see cref="ListAsync"/>.
        /// </summary>
        Task<Voice> GetAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes a stored custom voice from a written description (voice design). The result has the
        /// <c>voice_...</c> id and a preview clip. Live-verified on 2026-10-10.
        /// </summary>
        Task<Voice> CreateDesignedAsync(DesignedVoiceRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes a stored custom voice by copying a real speaker from two recordings (voice replication).
        /// <b>Only use recordings of a speaker who has agreed.</b> Google requires a consent recording, and
        /// this library does not check or fake it. This method follows Google's documented request shape and
        /// has not been run against the real API by this library's authors, because a test needs a real
        /// person's voice and consent. The stateless <c>voicekey_</c> variant is not supported.
        /// </summary>
        Task<Voice> CreateReplicatedAsync(ReplicatedVoiceRequest request, CancellationToken cancellationToken = default);

        /// <summary>Deletes one of your stored custom voices. A prebuilt voice or an unknown id returns HTTP 404.</summary>
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}
