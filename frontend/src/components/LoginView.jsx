export function LoginView({
  loginEmail,
  loginPassword,
  errorMessage,
  onEmailChange,
  onPasswordChange,
  onSubmit,
  onRegister,
}) {
  return (
    <main className="mx-auto flex min-h-screen max-w-5xl items-center justify-center p-6">
      <section className="w-full max-w-xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <p className="mb-2 text-sm font-medium text-blue-700">Cloud Native Image Processing</p>
        <h1 className="text-3xl font-semibold text-slate-900">
          Sign in to manage your image library
        </h1>
        <p className="mt-3 text-slate-600">
          Sign in with ASP.NET Core Identity (local API). Create an account or log in, then manage
          your image library.
        </p>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            onSubmit();
          }}
          className="mt-6 space-y-3"
        >
          <input
            type="email"
            placeholder="Email"
            value={loginEmail}
            onChange={(event) => onEmailChange(event.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            required
          />
          <input
            type="password"
            placeholder="Password"
            value={loginPassword}
            onChange={(event) => onPasswordChange(event.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            required
          />
          {errorMessage && (
            <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {errorMessage}
            </p>
          )}
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="submit"
              className="rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
            >
              Login with Email
            </button>
            <button
              type="button"
              onClick={onRegister}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 font-medium text-slate-700 hover:bg-slate-50"
            >
              Register with Email
            </button>
          </div>
        </form>
      </section>
    </main>
  );
}
