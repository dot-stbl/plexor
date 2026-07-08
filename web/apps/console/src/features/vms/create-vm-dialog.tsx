import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { Button } from '@/shared/ui/primitives/button';
import { Field, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { FieldDescription } from '@/shared/ui/primitives/field';

interface CreateVmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const ZONES = ['eu-central-1', 'eu-west-1', 'us-east-1'];
const MACHINES = ['small (2 vCPU / 4 GB)', 'medium (4 vCPU / 8 GB)', 'large (8 vCPU / 16 GB)'];

/**
 * MVP stub — full multi-step wizard is a separate plan. Three fields,
 * submit intentionally disabled until the wizard lands.
 */
export function CreateVmDialog({ open, onOpenChange }: CreateVmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Создать виртуальную машину</DialogTitle>
          <DialogDescription>
            Multi-step wizard появится в Screen 03. Здесь показываем только форму параметров.
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="create-vm-name">Имя</FieldLabel>
            <Input id="create-vm-name" placeholder="например, web-prod-02" />
          </Field>

          <Field>
            <FieldLabel>Зона</FieldLabel>
            <Select items={ZONES.map((z) => ({ value: z, label: z }))}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Выберите зону" />
              </SelectTrigger>
              <SelectContent>
                {ZONES.map((zone) => (
                  <SelectItem key={zone} value={zone}>
                    {zone}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <Field>
            <FieldLabel>Флейвор</FieldLabel>
            <Select items={MACHINES.map((m) => ({ value: m, label: m }))}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Выберите флейвор" />
              </SelectTrigger>
              <SelectContent>
                {MACHINES.map((machine) => (
                  <SelectItem key={machine} value={machine}>
                    {machine}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <FieldDescription>
              Подробный wizard с образами, сетью и ключами — в следующем релизе.
            </FieldDescription>
          </Field>
        </FieldGroup>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button disabled>Создать</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}