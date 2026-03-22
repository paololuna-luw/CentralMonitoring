import { Routes } from '@angular/router';
import { TargetsComponent } from './pages/targets/targets.component';
import { MetricsComponent } from './pages/metrics/metrics.component';
import { AlertsComponent } from './pages/alerts/alerts.component';
import { RulesComponent } from './pages/rules/rules.component';
import { HomeComponent } from './pages/home/home.component';
import { HostsComponent } from './pages/hosts/hosts.component';

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'home', component: HomeComponent },
  { path: 'targets', component: TargetsComponent },
  { path: 'hosts', component: HostsComponent },
  { path: 'metrics', component: MetricsComponent },
  { path: 'alerts', component: AlertsComponent },
  { path: 'rules', component: RulesComponent }
];
