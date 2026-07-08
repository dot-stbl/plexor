import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, Plus } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { PageHeader } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';

export const Route = createFileRoute('/vms/new')({
  component: CreateVmPage,
});

const ZONES = [
  { value: 'eu-central-1', label: 'eu-central-1 — Frankfurt' },
  { value: 'eu-west-1', label: 'eu-west-1 — Dublin' },
  { value: 'us-east-1', label: 'us-east-1 — Ashburn' },
];

const FLAVORS = [
  { value: 'small', label: 'Small — 2 vCPU / 4 GB / 20 GB' },
  { value: 'medium', label: 'Medium — 4 vCPU / 8 GB / 40 GB' },
  { value: 'large', label: 'Large — 8 vCPU / 16 GB / 80 GB' },
];

/**
 * Step 1 of the Create VM flow. Full wizard (steps 2–6) is a separate plan;
 * the future resource-providers layer will build this page from a YAML
 * descriptor (`fields: [...]`), so the structure here intentionally leans
 * on FieldGroup + plain state so a YAML renderer can map directly.
 */
function CreateVmPage() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [zone, setZone] = useState<string>('');
  const [flavor, setFlavor] = useState<string>('');

  return (
    <main data-od-id="vms-new">
      <Breadcrumb className="mx-auto w-full max-w-3xl px-6 pt-6 lg:px-8">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/vms" />}>Виртуальные машины</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/vms/new" />}>Новая ВМ</BreadcrumbLink>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <PageHeader
        title="Создать виртуальную машину"
        description="Шаг 1 из 6 — Базовая конфигурация. Остальные шаги появятся в Screen 03."
        actions={
          <Button variant="ghost" render={<Link to="/vms" />}>
            <ArrowLeft />
            Назад
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-3xl px-6 py-6 lg:px-8">
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <CardTitle className="text-sm">Основные параметры</CardTitle>
            <CardDescription>Имя, зона и конфигурация CPU/RAM/диск.</CardDescription>
          </CardHeader>
          <CardContent className="p-4">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="create-vm-name">Имя</FieldLabel>
                <Input
                  id="create-vm-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder="например, web-prod-02"
                />
                <FieldDescription>
                  Используется как hostname. Строчные латинские буквы, цифры и дефис.
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel>Зона</FieldLabel>
                <Select
                  items={ZONES.map((z) => ({ value: z.value, label: z.label }))}
                  value={zone}
                  onValueChange={(value) => setZone(value ?? '')}
                >
                  <SelectTrigger id="create-vm-zone">
                    <SelectValue placeholder="Выберите зону" />
                  </SelectTrigger>
                  <SelectContent>
                    {ZONES.map((z) => (
                      <SelectItem key={z.value} value={z.value}>
                        {z.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>Регион и площадка, где будет размещена ВМ.</FieldDescription>
              </Field>

              <Field>
                <FieldLabel>Флейвор</FieldLabel>
                <Select
                  items={FLAVORS.map((f) => ({ value: f.value, label: f.label }))}
                  value={flavor}
                  onValueChange={(value) => setFlavor(value ?? '')}
                >
                  <SelectTrigger id="create-vm-flavor">
                    <SelectValue placeholder="Выберите конфигурацию" />
                  </SelectTrigger>
                  <SelectContent>
                    {FLAVORS.map((f) => (
                      <SelectItem key={f.value} value={f.value}>
                        {f.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>
                  CPU, RAM и размер диска. Стоимость рассчитывается автоматически.
                </FieldDescription>
              </Field>
            </FieldGroup>
          </CardContent>
        </Card>

        <div className="mt-4 flex items-center justify-between">
          <Button variant="outline" render={<Link to="/vms" />}>
            Отмена
          </Button>
          <Button onClick={() => navigate({ to: '/vms' })} disabled={!name || !zone || !flavor}>
            <Plus />
            Далее: Образ
          </Button>
        </div>
      </div>
    </main>
  );
}