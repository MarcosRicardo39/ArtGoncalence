import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ContaApi } from './conta-api';
import { routes } from './app.routes';

describe('Conta e perfil', () => {
  let http: HttpTestingController;
  let api: ContaApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(ContaApi);
  });

  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=;max-age=0;path=/';
    http.verify();
  });

  it('inclui CSRF e mantém a sessão somente em memória', async () => {
    document.cookie = 'XSRF-TOKEN=token-de-teste;path=/';
    const autenticacao = api.autenticar('artista@example.test', 'senha fictícia');
    http.expectOne('/api/contas/csrf').flush(null);
    await Promise.resolve();

    const login = http.expectOne('/api/contas/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.headers.get('X-XSRF-TOKEN')).toBe('token-de-teste');
    expect(login.request.body).toEqual({ email: 'artista@example.test', senha: 'senha fictícia' });
    login.flush(null);
    await Promise.resolve();
    await Promise.resolve();

    http.expectOne('/api/contas/sessao').flush({ id: 'artista', emailConfirmado: false });
    await autenticacao;
    expect(api.sessao()?.id).toBe('artista');

    const logout = api.sair();
    http.expectOne('/api/contas/csrf').flush(null);
    await Promise.resolve();
    http.expectOne('/api/contas/sair').flush(null);
    await logout;
    expect(api.sessao()).toBeNull();
  });

  it('envia somente os campos editáveis do próprio perfil', async () => {
    const edicao = api.editarPerfil('Artista de teste', null);
    http.expectOne('/api/contas/csrf').flush(null);
    await Promise.resolve();
    const perfil = http.expectOne('/api/artistas/me');
    expect(perfil.request.method).toBe('PUT');
    expect(perfil.request.body).toEqual({ nomePublico: 'Artista de teste', bio: null });
    perfil.flush(null);
    await edicao;
  });

  it('apresenta cadastro acessível e valida campos antes de enviar', async () => {
    const harness = await RouterTestingHarness.create('/conta/cadastro');
    const tela = harness.routeNativeElement!;
    expect(tela.querySelector('h1')?.textContent).toBe('Crie sua conta');
    expect(tela.querySelector('label[for="email"]')?.textContent).toBe('E-mail');
    tela
      .querySelector('form')
      ?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    harness.detectChanges();
    expect(tela.querySelector('#email-erro')?.textContent).toContain('Informe um e-mail válido.');
    expect(tela.querySelector('#senha-erro')?.textContent).toContain('15 a 128 caracteres');
    http.expectNone('/api/contas/csrf');
  });

  it('redireciona o perfil de edição quando a sessão expirou', async () => {
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/conta/perfil');
    const sessao = await vi.waitFor(() => http.expectOne('/api/contas/sessao'));
    sessao.flush(null, { status: 401, statusText: 'Unauthorized' });
    await navegacao;
    await vi.waitFor(() =>
      expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(
        'Entre na sua conta',
      ),
    );
  });
});
