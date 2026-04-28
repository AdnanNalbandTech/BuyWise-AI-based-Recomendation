import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthService } from './core/auth.service';

const requireAuth: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.parseUrl('/login');
};

const requireAdmin: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAdmin() ? true : router.parseUrl('/shop');
};

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./pages/register/register.component').then((m) => m.RegisterComponent)
  },
  {
    path: 'shop',
    canActivate: [requireAuth],
    loadComponent: () => import('./pages/shop/shop.component').then((m) => m.ShopComponent)
  },
  {
    path: 'products/:id',
    canActivate: [requireAuth],
    loadComponent: () => import('./pages/product-detail/product-detail.component').then((m) => m.ProductDetailComponent)
  },
  {
    path: 'cart',
    canActivate: [requireAuth],
    loadComponent: () => import('./pages/cart/cart.component').then((m) => m.CartComponent)
  },
  {
    path: 'admin',
    canActivate: [requireAdmin],
    loadComponent: () => import('./pages/admin/admin.component').then((m) => m.AdminComponent)
  },
  { path: '**', redirectTo: 'login' }
];
