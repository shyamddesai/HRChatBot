import { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Bot, Users, LogOut, Plus, RefreshCcw, AlertCircle, X, CheckCircle, 
  UserPlus, MoreHorizontal, UserX, Search, Filter, ArrowUpDown, 
  Building2, Mail, Calendar, Briefcase, FileText, 
  Edit3, UserCheck, ChevronDown, ChevronUp, TrendingUp, History
} from 'lucide-react';
import api from '../api';
import Chat from './Chat';

interface Employee {
  id: string;
  fullName: string;
  email: string;
  department: string;
  grade: string;
  status: string;
  employeeCode: string;
  hireDate: string;
  role: string;
  managerId?: string;
  managerName?: string;
}

interface UserInfo {
  role: string;
  fullName: string;
}

type SortField = 'fullName' | 'department' | 'grade' | 'hireDate' | 'status';
type SortOrder = 'asc' | 'desc';

export default function Dashboard() {
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDeactivateModal, setShowDeactivateModal] = useState(false);
  const [showReactivateModal, setShowReactivateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [selectedEmployee, setSelectedEmployee] = useState<Employee | null>(null);
  const [showActionsMenu, setShowActionsMenu] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [departmentFilter, setDepartmentFilter] = useState<string>('all');
  const [sortField, setSortField] = useState<SortField>('fullName');
  const [sortOrder, setSortOrder] = useState<SortOrder>('asc');
  const [showFilters, setShowFilters] = useState(false);
  const [showPromoteModal, setShowPromoteModal] = useState(false);
  const [showSalaryHistoryModal, setShowSalaryHistoryModal] = useState(false);
  const [salaryHistory, setSalaryHistory] = useState<any[]>([]);
  const queryClient = useQueryClient();
  const dropdownRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  const userStr = localStorage.getItem('user');
  const user: UserInfo | null = userStr ? JSON.parse(userStr) : null;
  const isHR = user?.role === 'HR';

  // Form state for new employee
  const [createFormData, setCreateFormData] = useState({
    fullName: '',
    email: '',
    department: '',
    grade: 'Grade 5',
    baseSalary: '',
    role: 'Employee'
  });

  // Form state for editing employee
  const [editFormData, setEditFormData] = useState({
    fullName: '',
    email: '',
    department: '',
    grade: '',
    newSalary: ''
  });

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
  };

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (!showActionsMenu) return;

      // Approximate scrollbar width – if click is near the right edge, treat as scrollbar click and ignore
      const scrollbarWidth = 20;
      const isScrollbarClick = event.clientX > window.innerWidth - scrollbarWidth;
      if (isScrollbarClick) return;

      const target = event.target as Node;
      if (dropdownRef.current && !dropdownRef.current.contains(target) &&
          buttonRef.current && !buttonRef.current.contains(target)) {
        setShowActionsMenu(null);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showActionsMenu, setShowActionsMenu]);

  const { data: employees, isLoading: employeesLoading } = useQuery({
    queryKey: ['employees', includeInactive],
    queryFn: async () => {
      const endpoint = includeInactive ? '/employees/all' : '/employees';
      const res = await api.get(endpoint);
      return res.data as Employee[];
    },
    enabled: isHR
  });

  const { data: myProfile } = useQuery({
    queryKey: ['profile'],
    queryFn: async () => {
      const res = await api.get('/employees/me');
      return res.data;
    }
  });

  // Get unique departments for filter
  const departments = [...new Set(employees?.map(e => e.department) || [])].sort();

  // Create employee mutation
  const createMutation = useMutation({
    mutationFn: async (data: typeof createFormData) => {
      const res = await api.post('/employees', {
        ...data,
        baseSalary: parseFloat(data.baseSalary) || 0
      });
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      setShowCreateModal(false);
      setCreateFormData({ fullName: '', email: '', department: '', grade: 'Grade 5', baseSalary: '', role: 'Employee' });
    }
  });

  // Update employee mutation
  const [currentSalary, setCurrentSalary] = useState<{baseSalary: number; currency: string} | null>(null);

  const updateMutation = useMutation({
    mutationFn: async ({ id, data }: { id: string; data: typeof editFormData }) => {
      const newSalaryValue = data.newSalary ? parseFloat(data.newSalary) : null;
      const currentValue = currentSalary?.baseSalary;
      
      // Only send salary if it was actually changed
      const salaryToSend = (newSalaryValue && newSalaryValue !== currentValue) 
        ? newSalaryValue 
        : null;
      
      const payload = {
        fullName: data.fullName,
        email: data.email,
        department: data.department,
        grade: data.grade,
        newSalary: salaryToSend
      };
      
      const res = await api.put(`/employees/${id}`, payload)
      return res.data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      setShowEditModal(false);
      setSelectedEmployee(null);
      setCurrentSalary(null);
      if (data.salaryUpdated) {
        alert('Employee updated successfully!');
      }
    }
  });

  const promoteMutation = useMutation({
    mutationFn: async ({ id, newGrade, newSalary }: { id: string; newGrade: string; newSalary?: number }) => {
      const payload = {
        newGrade,
        newSalary: newSalary || 0
      };
      const res = await api.put(`/employees/${id}/promote`, payload);
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      setShowPromoteModal(false);
      setSelectedEmployee(null);
    }
  });

  // Deactivate mutation
  const deactivateMutation = useMutation({
    mutationFn: async (id: string) => {
      const res = await api.post(`/employees/${id}/archive`, {});
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] });
      setShowDeactivateModal(false);
      setSelectedEmployee(null);
      setShowActionsMenu(null);
    }
  });

  // Restore mutation
  const restoreMutation = useMutation({
    mutationFn: async (id: string) => {
      const res = await api.post(`/employees/${id}/restore`, {});
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'], exact: false });
      
      if (selectedEmployee) {
        queryClient.invalidateQueries({ queryKey: ['profile'] });
      }

      setShowReactivateModal(false);
      setSelectedEmployee(null);
      setShowActionsMenu(null);
    }
  });

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate(createFormData);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedEmployee) {
      updateMutation.mutate({ id: selectedEmployee.id, data: editFormData });
    }
  };

  const handleDeactivate = () => {
    if (selectedEmployee) {
      deactivateMutation.mutate(selectedEmployee.id);
    }
  };

  const handleReactivate = () => {
    if (selectedEmployee) {
      restoreMutation.mutate(selectedEmployee.id);
    }
  };

  const openEditModal = async (emp: Employee) => {
    setSelectedEmployee(emp);
    
    try {
      // Fetch full employee details including salaries
      const res = await api.get(`/employees/${emp.id}`);
      const employeeData = res.data;
      
      // Find current salary (effectiveTo == null)
      const currentSalary = employeeData.salaries?.find((s: any) => !s.effectiveTo)?.baseSalary || 0;
      
      setCurrentSalary({ baseSalary: currentSalary, currency: 'AED' });
      setEditFormData({
        fullName: emp.fullName,
        email: emp.email,
        department: emp.department,
        grade: emp.grade,
        newSalary: currentSalary > 0 ? currentSalary.toString() : ''
      });
    } catch (error) {
      console.error('Failed to fetch employee details', error);
      setCurrentSalary(null);
      setEditFormData({
        fullName: emp.fullName,
        email: emp.email,
        department: emp.department,
        grade: emp.grade,
        newSalary: ''
      });
    }

    setShowEditModal(true);
    setShowActionsMenu(null);
  };

  const openReactivateModal = (emp: Employee) => {
    setSelectedEmployee(emp);
    setShowReactivateModal(true);
    setShowActionsMenu(null);
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortOrder('asc');
    }
  };

  // Filter and sort employees
  const processedEmployees = employees
    ?.filter(emp => {
      const matchesSearch = 
        emp.fullName.toLowerCase().includes(searchQuery.toLowerCase()) ||
        emp.email.toLowerCase().includes(searchQuery.toLowerCase()) ||
        emp.department.toLowerCase().includes(searchQuery.toLowerCase()) ||
        emp.employeeCode.toLowerCase().includes(searchQuery.toLowerCase()) ||
        emp.grade.toLowerCase().includes(searchQuery.toLowerCase());
      
      const matchesDepartment = departmentFilter === 'all' || emp.department === departmentFilter;
      
      return matchesSearch && matchesDepartment;
    })
    ?.sort((a, b) => {
      let comparison = 0;
      switch (sortField) {
        case 'fullName':
          comparison = a.fullName.localeCompare(b.fullName);
          break;
        case 'department':
          comparison = a.department.localeCompare(b.department);
          break;
        case 'grade':
          comparison = a.grade.localeCompare(b.grade);
          break;
        case 'hireDate':
          comparison = new Date(a.hireDate).getTime() - new Date(b.hireDate).getTime();
          break;
        case 'status':
          comparison = a.status.localeCompare(b.status);
          break;
      }
      return sortOrder === 'asc' ? comparison : -comparison;
    });

  const activeCount = employees?.filter(e => e.status === 'Active').length ?? 0;
  const inactiveCount = employees?.filter(e => e.status !== 'Active').length ?? 0;

  // Stats for HR view
  const hrStats = [
    { label: 'Total Active', value: activeCount, icon: Users, color: 'bg-blue-500' },
    { label: 'Departments', value: departments.length, icon: Building2, color: 'bg-purple-500' },
  ];

  // Stats for Employee view
  const employeeStats = [
    { label: 'My Department', value: myProfile?.department ?? '-', icon: Users, color: 'bg-green-500' },
    { label: 'My Grade', value: myProfile?.grade ?? '-', icon: Briefcase, color: 'bg-purple-500' },
  ];

  const stats = isHR ? hrStats : employeeStats;

  const SortIcon = ({ field }: { field: SortField }) => {
    if (sortField !== field) return <ArrowUpDown size={14} className="text-gray-400" />;
    return sortOrder === 'asc' ? 
      <ChevronUp size={14} className="text-blue-600" /> : 
      <ChevronDown size={14} className="text-blue-600" />;
  };

  return (
    <div className="flex h-screen relative overflow-hidden bg-gray-50 dark:bg-gray-900">
      {/* Main Content */}
      <div className={`flex-1 transition-all duration-300 ${isChatOpen ? 'pr-[400px]' : ''} overflow-y-auto`}>
        {/* Header */}
        <header className="bg-white dark:bg-gray-800 shadow-sm sticky top-0 z-10">
          <div className="px-6 py-4 flex justify-between items-center">
            <div>
              <h1 className="text-2xl font-bold text-gray-800 dark:text-white">HR Dashboard</h1>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Welcome back, {user?.fullName} {isHR && <span className="text-blue-500 font-medium">(HR Admin)</span>}
              </p>
            </div>
            <button
              onClick={handleLogout}
              className="p-2 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-full"
            >
              <LogOut size={20} />
            </button>
          </div>
        </header>

        <main className="p-6">
          {/* Stats */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            {stats.map((stat, idx) => (
              <div key={idx} className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex items-center">
                <div className={`${stat.color} w-12 h-12 rounded-lg flex items-center justify-center text-white mr-4`}>
                  <stat.icon size={24} />
                </div>
                <div>
                  <p className="text-sm text-gray-600 dark:text-gray-400">{stat.label}</p>
                  <p className="text-2xl font-semibold text-gray-800 dark:text-white">{stat.value}</p>
                </div>
              </div>
            ))}
          </div>

          {/* Quick Actions */}
          {isHR && (
            <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4 mb-8">
              <h3 className="text-sm font-semibold text-blue-900 dark:text-blue-300 mb-3">Quick Actions</h3>
              <div className="flex flex-wrap gap-3">
                <button 
                  onClick={() => setShowCreateModal(true)}
                  className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition"
                >
                  <Plus size={16} />
                  New Employee
                </button>
              </div>
            </div>
          )}

          {/* Employee Table */}
          {isHR && (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
                <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-semibold text-gray-800 dark:text-white">Employees</h2>
                    <p className="text-sm text-gray-500">
                      {activeCount} active
                      {includeInactive && inactiveCount > 0 && `, ${inactiveCount} inactive`}
                      {' '}({processedEmployees?.length ?? 0} shown)
                    </p>
                  </div>
                  
                  <div className="flex flex-wrap items-center gap-3">
                    {/* Filter Button */}
                    <button
                      onClick={() => setShowFilters(!showFilters)}
                      className={`flex items-center gap-2 px-3 py-2 text-sm rounded-lg border transition ${
                        showFilters || departmentFilter !== 'all'
                          ? 'bg-blue-50 border-blue-300 text-blue-700 dark:bg-blue-900/20 dark:border-blue-700 dark:text-blue-300'
                          : 'bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300'
                      }`}
                    >
                      <Filter size={16} />
                      Filters
                      {departmentFilter !== 'all' && <span className="ml-1 w-2 h-2 bg-blue-500 rounded-full"></span>}
                    </button>

                    {/* Include Inactive Checkbox */}
                    <label className="flex items-center gap-2 cursor-pointer select-none">
                      <div className="relative">
                        <input
                          type="checkbox"
                          checked={includeInactive}
                          onChange={(e) => setIncludeInactive(e.target.checked)}
                          className="sr-only peer"
                        />
                        <div className="w-5 h-5 border-2 border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 peer-checked:bg-blue-600 peer-checked:border-blue-600 transition-all"></div>
                        <div className="absolute inset-0 flex items-center justify-center text-white opacity-0 peer-checked:opacity-100 transition-opacity">
                          <CheckCircle size={12} />
                        </div>
                      </div>
                      <span className="text-sm text-gray-700 dark:text-gray-300">Include inactive</span>
                    </label>
                    
                    {/* Search */}
                    <div className="relative">
                      <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={16} />
                      <input
                        type="text"
                        placeholder="Search employees..."
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        className="pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white w-64"
                      />
                    </div>
                  </div>
                </div>

                {/* Expanded Filters */}
                {showFilters && (
                  <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700 flex flex-wrap items-center gap-4">
                    <div className="flex items-center gap-2">
                      <label className="text-sm text-gray-600 dark:text-gray-400">Department:</label>
                      <select
                        value={departmentFilter}
                        onChange={(e) => setDepartmentFilter(e.target.value)}
                        className="px-3 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                      >
                        <option value="all">All Departments</option>
                        {departments.map(dept => (
                          <option key={dept} value={dept}>{dept}</option>
                        ))}
                      </select>
                    </div>
                    
                    {departmentFilter !== 'all' && (
                      <button
                        onClick={() => setDepartmentFilter('all')}
                        className="text-sm text-blue-600 hover:text-blue-800 dark:text-blue-400"
                      >
                        Clear filter
                      </button>
                    )}
                  </div>
                )}
              </div>

                <div className="overflow-x-auto max-h-[600px] overflow-y-auto">
                <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                  <thead className="sticky top-0 z-10 bg-white dark:bg-gray-800">
                    <tr>
                      <th 
                        className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600"
                        onClick={() => handleSort('fullName')}
                      >
                        <div className="flex items-center gap-1">
                          Employee
                          <SortIcon field="fullName" />
                        </div>
                      </th>
                      <th 
                        className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600"
                        onClick={() => handleSort('department')}
                      >
                        <div className="flex items-center gap-1">
                          <Building2 size={12} />
                          Department
                          <SortIcon field="department" />
                        </div>
                      </th>
                      <th 
                        className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600"
                        onClick={() => handleSort('grade')}
                      >
                        <div className="flex items-center gap-1">
                          <Briefcase size={12} />
                          Grade
                          <SortIcon field="grade" />
                        </div>
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">
                        <div className="flex items-center gap-1">
                          <Mail size={12} />
                          Contact
                        </div>
                      </th>
                      <th 
                        className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600"
                        onClick={() => handleSort('hireDate')}
                      >
                        <div className="flex items-center gap-1">
                          <Calendar size={12} />
                          Hire Date
                          <SortIcon field="hireDate" />
                        </div>
                      </th>
                      <th 
                        className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600"
                        onClick={() => handleSort('status')}
                      >
                        <div className="flex items-center gap-1">
                          Status
                          <SortIcon field="status" />
                        </div>
                      </th>
                      <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase"></th>
                    </tr>
                  </thead>
                  <tbody className="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
                    {employeesLoading ? (
                      <tr><td colSpan={7} className="px-6 py-8 text-center text-gray-500">
                        <div className="flex justify-center"><RefreshCcw className="animate-spin" /></div>
                      </td></tr>
                    ) : processedEmployees?.length === 0 ? (
                      <tr><td colSpan={7} className="px-6 py-8 text-center text-gray-500">
                        {searchQuery || departmentFilter !== 'all' ? 'No employees match your filters' : 'No employees found'}
                      </td></tr>
                    ) : processedEmployees?.map((emp) => (
                      <tr key={emp.id} className={`hover:bg-gray-50 dark:hover:bg-gray-700/50 ${emp.status !== 'Active' ? 'opacity-60' : ''}`}>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex items-center">
                            <div className={`w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-medium mr-3 ${
                              emp.status === 'Active' 
                                ? 'bg-gradient-to-br from-blue-400 to-blue-600' 
                                : 'bg-gradient-to-br from-gray-400 to-gray-600'
                            }`}>
                              {emp.fullName.charAt(0)}
                            </div>
                            <div>
                              <div className="text-sm font-medium text-gray-900 dark:text-white">{emp.fullName}</div>
                              <div className="text-xs text-gray-500 dark:text-gray-400 font-mono">{emp.employeeCode}</div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-700 dark:text-gray-300">
                          <span className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded text-xs">
                            {emp.department}
                          </span>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-700 dark:text-gray-300 font-medium">
                          {emp.grade}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">
                          <div className="text-xs">{emp.email}</div>
                          {emp.managerName && (
                            <div className="text-xs text-gray-400 mt-1">Reports to: {emp.managerName}</div>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">
                          {new Date(emp.hireDate).toLocaleDateString('en-US', { 
                            year: 'numeric', 
                            month: 'short', 
                            day: 'numeric' 
                          })}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <span className={`px-2.5 py-1 inline-flex text-xs leading-5 font-semibold rounded-full ${
                            emp.status === 'Active' 
                              ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400' 
                              : 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-400'
                          }`}>
                            {emp.status === 'Active' ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium relative">
                          <button
                            ref={buttonRef}
                            onClick={() => setShowActionsMenu(showActionsMenu === emp.id ? null : emp.id)}
                            className="p-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700"
                          >
                            <MoreHorizontal size={18} />
                          </button>
                          
                          {/* Actions Dropdown */}
                          {showActionsMenu === emp.id && (
                            <div
                              ref={dropdownRef}
                              className="absolute right-0 mt-2 w-48 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 z-20 py-1"
                            >
                              <button
                                onClick={() => openEditModal(emp)}
                                className="w-full px-4 py-2 text-left text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-2"
                              >
                                <Edit3 size={16} />
                                Edit Details
                              </button>

                              <button
                                onClick={() => {
                                  setSelectedEmployee(emp);
                                  setShowPromoteModal(true);
                                  setShowActionsMenu(null);
                                }}
                                className="w-full px-4 py-2 text-left text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-2"
                              >
                                <TrendingUp size={16} />
                                Promote
                              </button>

                              {/* Salary Certificate */}
                              <button
                                onClick={async () => {
                                  try {
                                    const response = await api.get(`/SalaryCertificate/${emp.id}`, { responseType: 'blob' });
                                    const url = window.URL.createObjectURL(new Blob([response.data]));
                                    const link = document.createElement('a');
                                    link.href = url;
                                    link.setAttribute('download', `Salary_Certificate_${emp.employeeCode}.pdf`);
                                    document.body.appendChild(link);
                                    link.click();
                                    link.remove();
                                    window.URL.revokeObjectURL(url);
                                  } catch (error) {
                                    console.error('Download failed', error);
                                    alert('Failed to generate certificate. Please try again.');
                                  }
                                  setShowActionsMenu(null);
                                }}
                                className="w-full px-4 py-2 text-left text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-2"
                              >
                                <FileText size={16} />
                                Salary Certificate
                              </button>

                              <button
                                onClick={async () => {
                                  setSelectedEmployee(emp);
                                  try {
                                    const res = await api.get(`/employees/${emp.id}`);
                                    setSalaryHistory(res.data.salaries || []);
                                    setShowSalaryHistoryModal(true);
                                  } catch (error) {
                                    console.error('Failed to fetch salary history', error);
                                  }
                                  setShowActionsMenu(null);
                                }}
                                className="w-full px-4 py-2 text-left text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-2"
                              >
                                <History size={16} />
                                Salary History
                              </button>
                              
                              {emp.status === 'Active' ? (
                                <button
                                  onClick={() => {
                                    setSelectedEmployee(emp);
                                    setShowDeactivateModal(true);
                                    setShowActionsMenu(null);
                                  }}
                                  className="w-full px-4 py-2 text-left text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 flex items-center gap-2"
                                >
                                  <UserX size={16} />
                                  Deactivate
                                </button>
                              ) : (
                                <button
                                  onClick={() => openReactivateModal(emp)}
                                  className="w-full px-4 py-2 text-left text-sm text-green-600 dark:text-green-400 hover:bg-green-50 dark:hover:bg-green-900/20 flex items-center gap-2"
                                >
                                  <UserCheck size={16} />
                                  Reactivate
                                </button>
                              )}
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* My Profile - Employee View */}
          {!isHR && myProfile && (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
                <h2 className="text-lg font-semibold text-gray-800 dark:text-white">My Profile</h2>
              </div>
              <div className="p-6 grid grid-cols-2 gap-6">
                <div>
                  <label className="text-xs text-gray-500 uppercase tracking-wide">Full Name</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">{myProfile.fullName}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase tracking-wide">Email</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">{myProfile.email}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase tracking-wide">Department</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">{myProfile.department}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase tracking-wide">Grade</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">{myProfile.grade}</p>
                </div>
              </div>
            </div>
          )}
        </main>
      </div>

      {/* Floating Chat Button */}
      {!isChatOpen && (
        <button
          onClick={() => setIsChatOpen(true)}
          className="fixed bottom-6 right-6 w-14 h-14 bg-blue-600 hover:bg-blue-700 rounded-full shadow-lg flex items-center justify-center text-white hover:scale-110 transition-all duration-200 active:scale-95 z-40"
        >
          <Bot size={28} />
        </button>
      )}

      {/* Chat Sidebar */}
      <div className={`fixed top-0 right-0 h-full w-[400px] transform transition-transform duration-300 ease-in-out z-50 ${
        isChatOpen ? 'translate-x-0' : 'translate-x-full'
      }`}>
        <Chat onClose={() => setIsChatOpen(false)} />
      </div>

      {/* Create Employee Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
            <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
                <UserPlus size={20} className="text-blue-500" />
                Create New Employee
              </h3>
              <button onClick={() => setShowCreateModal(false)} className="text-gray-400 hover:text-gray-600">
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateSubmit} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Full Name *</label>
                <input
                  type="text"
                  required
                  value={createFormData.fullName}
                  onChange={(e) => setCreateFormData({...createFormData, fullName: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  placeholder="John Doe"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Email *</label>
                <input
                  type="email"
                  required
                  value={createFormData.email}
                  onChange={(e) => setCreateFormData({...createFormData, email: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  placeholder="john.doe@company.com"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Department *</label>
                <select
                  required
                  value={createFormData.department}
                  onChange={(e) => setCreateFormData({...createFormData, department: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                >
                  <option value="">Select Department</option>
                  <option value="IT">IT</option>
                  <option value="HR">HR</option>
                  <option value="Finance">Finance</option>
                  <option value="Sales">Sales</option>
                  <option value="Marketing">Marketing</option>
                  <option value="Operations">Operations</option>
                </select>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Grade</label>
                  <select
                    value={createFormData.grade}
                    onChange={(e) => setCreateFormData({...createFormData, grade: e.target.value})}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  >
                    {[...Array(15)].map((_, i) => (
                      <option key={i} value={`Grade ${i + 1}`}>Grade {i + 1}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Role</label>
                  <select
                    value={createFormData.role}
                    onChange={(e) => setCreateFormData({...createFormData, role: e.target.value})}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  >
                    <option value="Employee">Employee</option>
                    <option value="HR">HR</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Base Salary (AED)</label>
                <input
                  type="number"
                  value={createFormData.baseSalary}
                  onChange={(e) => setCreateFormData({...createFormData, baseSalary: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  placeholder="10000"
                />
              </div>

              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="flex-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition disabled:opacity-50 flex items-center justify-center gap-2"
                >
                  {createMutation.isPending ? <RefreshCcw className="animate-spin" size={16} /> : <CheckCircle size={16} />}
                  {createMutation.isPending ? 'Creating...' : 'Create Employee'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Employee Modal */}
      {showEditModal && selectedEmployee && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
            <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
                <Edit3 size={20} className="text-blue-500" />
                Edit Employee
              </h3>
              <button onClick={() => setShowEditModal(false)} className="text-gray-400 hover:text-gray-600">
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleEditSubmit} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Full Name</label>
                <input
                  type="text"
                  required
                  value={editFormData.fullName}
                  onChange={(e) => setEditFormData({...editFormData, fullName: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Email</label>
                <input
                  type="email"
                  required
                  value={editFormData.email}
                  onChange={(e) => setEditFormData({...editFormData, email: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Department</label>
                <select
                  required
                  value={editFormData.department}
                  onChange={(e) => setEditFormData({...editFormData, department: e.target.value})}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                >
                  <option value="IT">IT</option>
                  <option value="HR">HR</option>
                  <option value="Finance">Finance</option>
                  <option value="Sales">Sales</option>
                  <option value="Marketing">Marketing</option>
                  <option value="Operations">Operations</option>
                </select>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Grade</label>
                  <select
                    value={editFormData.grade}
                    onChange={(e) => setEditFormData({...editFormData, grade: e.target.value})}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  >
                    {[...Array(15)].map((_, i) => (
                      <option key={i} value={`Grade ${i + 1}`}>Grade {i + 1}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                    Salary (AED)
                    {currentSalary && currentSalary.baseSalary > 0 && (
                      <span className="text-xs text-gray-500 font-normal ml-1">
                        (current: {currentSalary.baseSalary.toLocaleString()})
                      </span>
                    )}
                  </label>
                  <input
                    type="number"
                    value={editFormData.newSalary}
                    onChange={(e) => setEditFormData({...editFormData, newSalary: e.target.value})}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
                  />
                </div>
              </div>

              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={() => setShowEditModal(false)}
                  className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateMutation.isPending}
                  className="flex-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition disabled:opacity-50 flex items-center justify-center gap-2"
                >
                  {updateMutation.isPending ? <RefreshCcw className="animate-spin" size={16} /> : <CheckCircle size={16} />}
                  {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Deactivate Confirmation Modal */}
      {showDeactivateModal && selectedEmployee && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-sm w-full p-6">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
                <AlertCircle className="text-red-600 dark:text-red-400" size={20} />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Deactivate Employee?</h3>
            </div>

            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Are you sure you want to deactivate <strong>{selectedEmployee.fullName}</strong> ({selectedEmployee.employeeCode})? 
              They will no longer have access to the system, but their records will be preserved for compliance.
            </p>

            <div className="flex gap-3">
              <button
                onClick={() => setShowDeactivateModal(false)}
                className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
              >
                Cancel
              </button>
              <button
                onClick={handleDeactivate}
                disabled={deactivateMutation.isPending}
                className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {deactivateMutation.isPending ? <RefreshCcw className="animate-spin" size={16} /> : <UserX size={16} />}
                {deactivateMutation.isPending ? 'Deactivating...' : 'Deactivate'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Reactivate Confirmation Modal */}
      {showReactivateModal && selectedEmployee && (
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
              <UserCheck className="text-green-600 dark:text-green-400" size={20} />
            </div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Reactivate Employee?</h3>
          </div>

          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4 mb-4">
            <h4 className="text-sm font-medium text-gray-900 dark:text-white mb-3">Employee Details</h4>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-500 dark:text-gray-400">Name:</span>
                <span className="font-medium text-gray-900 dark:text-white">{selectedEmployee.fullName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500 dark:text-gray-400">Code:</span>
                <span className="font-mono text-gray-900 dark:text-white">{selectedEmployee.employeeCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500 dark:text-gray-400">Department:</span>
                <span className="text-gray-900 dark:text-white">{selectedEmployee.department}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500 dark:text-gray-400">Grade:</span>
                <span className="text-gray-900 dark:text-white">{selectedEmployee.grade}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500 dark:text-gray-400">Previous Hire Date:</span>
                <span className="text-gray-900 dark:text-white">
                  {new Date(selectedEmployee.hireDate).toLocaleDateString()}
                </span>
              </div>
            </div>
          </div>

          <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-3 mb-6">
            <p className="text-sm text-yellow-800 dark:text-yellow-300 flex items-start gap-2">
              <AlertCircle size={16} className="mt-0.5 flex-shrink-0" />
              <span>
                <strong>Note:</strong> Upon reactivation, the hire date will be updated to today ({
                  new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
                }) for compliance and reporting purposes.
              </span>
            </p>
          </div>

          <p className="text-gray-600 dark:text-gray-400 mb-6 text-sm">
            Please confirm these details are correct. The employee will regain full system access.
          </p>

          <div className="flex gap-3">
            <button
              onClick={() => setShowReactivateModal(false)}
              className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
            >
              Cancel
            </button>
            <button
              onClick={handleReactivate}
              disabled={restoreMutation.isPending}
              className="flex-1 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {restoreMutation.isPending ? <RefreshCcw className="animate-spin" size={16} /> : <UserCheck size={16} />}
              {restoreMutation.isPending ? 'Reactivating...' : 'Confirm Reactivation'}
            </button>
          </div>
        </div>
      </div>
    )}

    {/* Promote Modal */}
    {showPromoteModal && selectedEmployee && (
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
              <TrendingUp className="text-blue-600 dark:text-blue-400" size={20} />
            </div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Promote Employee</h3>
            <button onClick={() => setShowPromoteModal(false)} className="ml-auto text-gray-400 hover:text-gray-600">
              <X size={20} />
            </button>
          </div>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              const formData = new FormData(e.currentTarget);
              const newGrade = formData.get('newGrade') as string;
              const newSalary = formData.get('newSalary') as string;
              const salaryNum = newSalary ? parseFloat(newSalary) : undefined;
              promoteMutation.mutate({ id: selectedEmployee.id, newGrade, newSalary: salaryNum });
            }}
            className="space-y-4"
          >
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Current Grade</label>
              <p className="px-3 py-2 bg-gray-100 dark:bg-gray-700 rounded-lg text-gray-900 dark:text-white">
                {selectedEmployee.grade}
              </p>
            </div>

            <div>
              <label htmlFor="newGrade" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                New Grade *
              </label>
              <select
                id="newGrade"
                name="newGrade"
                required
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
              >
                <option value="">Select Grade</option>
                {[...Array(15)].map((_, i) => (
                  <option key={i} value={`Grade ${i + 1}`}>Grade {i + 1}</option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="newSalary" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                New Salary (AED) <span className="text-gray-500">(optional)</span>
              </label>
              <input
                type="number"
                id="newSalary"
                name="newSalary"
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                placeholder="Leave empty to keep current salary"
              />
            </div>

            <div className="flex gap-3 pt-4">
              <button
                type="button"
                onClick={() => setShowPromoteModal(false)}
                className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={promoteMutation.isPending}
                className="flex-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {promoteMutation.isPending ? <RefreshCcw className="animate-spin" size={16} /> : <CheckCircle size={16} />}
                {promoteMutation.isPending ? 'Promoting...' : 'Confirm Promotion'}
              </button>
            </div>
          </form>
        </div>
      </div>
    )}
        
    {/* Salary History Modal */}
    {showSalaryHistoryModal && selectedEmployee && (
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full max-h-[80vh] overflow-y-auto">
          <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center sticky top-0 bg-white dark:bg-gray-800">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
              <History size={20} className="text-blue-500" />
              Salary History – {selectedEmployee.fullName}
            </h3>
            <button onClick={() => setShowSalaryHistoryModal(false)} className="text-gray-400 hover:text-gray-600">
              <X size={20} />
            </button>
          </div>

          <div className="p-6 space-y-4">
            {salaryHistory.length === 0 ? (
              <p className="text-center text-gray-500">No salary records found.</p>
            ) : (
              <div className="space-y-3">
                {salaryHistory.map((sal: any) => (
                  <div key={sal.id} className="border border-gray-200 dark:border-gray-700 rounded-lg p-4">
                    <div className="flex justify-between items-center">
                      <span className="text-lg font-semibold text-gray-900 dark:text-white">
                        {sal.baseSalary.toLocaleString()} {sal.currency}
                      </span>
                      <span className="text-xs text-gray-500">
                        {new Date(sal.effectiveFrom).toLocaleDateString()}
                        {sal.effectiveTo && ` – ${new Date(sal.effectiveTo).toLocaleDateString()}`}
                        {!sal.effectiveTo && <span className="ml-2 text-green-600">(current)</span>}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    )}
  </div>
  );
}